using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using CyberManager.Server.Data;
using CyberManager.Server.Hubs;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>
/// Analiza el estado del servidor, los equipos y los registros con reglas fijas. Opcionalmente
/// envía un resumen sanitizado a un proveedor compatible con la API de chat de OpenAI para
/// obtener una explicación en lenguaje natural. El diagnóstico nunca ejecuta acciones.
/// </summary>
public sealed class TroubleshootService
{
    private static readonly Regex BearerRegex = new(
        @"Bearer\s+[A-Za-z0-9._\-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex JwtRegex = new(
        @"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex RifRegex = new(
        @"\b[VEJ]-?\d{7,9}-?\d\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IpRegex = new(
        @"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled);
    private static readonly Regex LongNumberRegex = new(
        @"\b\d{13,19}\b", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly ClientUpdateService _updates;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TroubleshootService> _log;

    public TroubleshootService(AppDbContext db, ClientUpdateService updates,
        IHttpClientFactory http, IConfiguration config, ILogger<TroubleshootService> log)
    {
        _db = db;
        _updates = updates;
        _http = http;
        _config = config;
        _log = log;
    }

    public async Task<TroubleshootReportDto> DiagnoseAsync(TroubleshootRequestDto request,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var offlineSeconds = _config.GetValue<int?>("Troubleshoot:OfflineThresholdSeconds") ?? 75;
        var maxLogLines = _config.GetValue<int?>("Troubleshoot:MaxLogLines") ?? 120;

        var stations = await _db.Stations.AsNoTracking()
            .Where(s => s.Active)
            .ToListAsync(ct);

        var activeSessions = await (from s in _db.Sessions.AsNoTracking()
                                    join st in _db.Stations.AsNoTracking() on s.StationId equals st.Id
                                    where st.Active &&
                                          (s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused)
                                    select new
                                    {
                                        s.Id,
                                        s.StationId,
                                        StationName = st.Name,
                                        s.StartedUtc,
                                        s.Status
                                    }).ToListAsync(ct);

        var updateStatus = await _updates.GetStatusAsync(_db, ct);

        var offline = stations.Where(s =>
            s.Status == StationStatus.Offline ||
            !s.LastSeenUtc.HasValue ||
            now - s.LastSeenUtc!.Value > TimeSpan.FromSeconds(offlineSeconds)).ToList();
        var offlineIds = offline.Select(s => s.Id).ToHashSet();
        var onlineCount = stations.Count - offline.Count;

        var findings = new List<TroubleshootFindingDto>();
        var logs = request.IncludeLogs
            ? await ReadRecentLogsAsync(maxLogLines, ct)
            : new List<string>();

        // --- Paquete y versiones del cliente ---
        if (!updateStatus.HasPackage || string.IsNullOrWhiteSpace(updateStatus.PackageVersion))
        {
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Critical,
                "El servidor no tiene paquete de cliente",
                "No se encontró ClientUpdate\\client.zip o su manifiesto, así que las estaciones no pueden actualizarse.",
                "Genere el instalador con scripts/build-installer.ps1 y publique el paquete en el servidor."));
        }

        if (updateStatus.Outdated > 0)
        {
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Warning,
                $"{updateStatus.Outdated} equipos con versión anterior",
                $"El paquete vigente es {updateStatus.PackageVersion ?? "desconocido"} y {updateStatus.Outdated} equipos reportan una versión menor.",
                "Ejecute «Actualizar archivos en clientes» desde la consola o el endpoint /api/client-update/notify."));
        }

        if (updateStatus.Unknown > 0)
        {
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Info,
                $"{updateStatus.Unknown} equipos sin versión reportada",
                "Esos equipos aún no han enviado su versión al servidor.",
                "Compruebe que el cliente esté abierto y con conexión al servidor."));
        }

        // --- Disponibilidad de equipos ---
        if (offline.Count > 0)
        {
            var names = string.Join(", ", offline.Take(8).Select(s => s.Name)) +
                        (offline.Count > 8 ? "…" : string.Empty);
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Warning,
                $"{offline.Count} equipos sin latido",
                $"No se ha recibido latido reciente de: {names}.",
                "Verifique red, firewall, que el agente esté en ejecución y que la hora del equipo sea correcta."));
        }

        var agentOffline = stations.Where(s => !AgentHub.IsConnected(s.Id)).ToList();
        if (agentOffline.Count > 0)
        {
            var names = string.Join(", ", agentOffline.Take(8).Select(s => s.Name)) +
                        (agentOffline.Count > 8 ? "…" : string.Empty);
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Critical,
                $"{agentOffline.Count} equipos sin agente conectado",
                $"El servicio CyberManagerAgent no tiene conexión SignalR en: {names}. " +
                "Las órdenes de apagado, reinicio y cierre de sesión se entregan solo a ese servicio.",
                $"Instale o actualice el agente {AppInfo.Version} en C:\\CyberManager\\Estacion\\Agent " +
                "y reinicie el servicio CyberManagerAgent. Confirme que su appsettings.json tenga el ID y token del equipo."));
        }

        var orphanSessions = activeSessions.Where(s => offlineIds.Contains(s.StationId)).ToList();
        if (orphanSessions.Count > 0)
        {
            var names = string.Join(", ", orphanSessions.Take(6).Select(s => s.StationName)) +
                        (orphanSessions.Count > 6 ? "…" : string.Empty);
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Critical,
                "Sesiones activas en equipos sin conexión",
                $"Hay sesiones abiertas o en pausa en equipos que no reportan latido: {names}.",
                "Revise esas sesiones desde la consola para evitar cobros incorrectos y confirme el estado real del equipo."));
        }

        var withoutMac = offline.Where(s => string.IsNullOrWhiteSpace(s.MacAddress)).ToList();
        if (withoutMac.Count > 0)
        {
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Info,
                $"{withoutMac.Count} equipos sin MAC registrada",
                "Wake on LAN no puede enviarse a equipos sin dirección MAC.",
                "Use «Escanear la red» o edite el equipo para registrar su MAC."));
        }

        var longSessions = activeSessions
            .Where(s => now - s.StartedUtc > TimeSpan.FromHours(8))
            .ToList();
        if (longSessions.Count > 0)
        {
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Info,
                $"{longSessions.Count} sesiones de más de 8 horas",
                "Hay sesiones abiertas desde hace más de ocho horas.",
                "Confirme que correspondan a usos legítimos o ciérrelas desde la consola."));
        }

        // --- Registros del servidor ---
        var errorLines = logs.Where(l => l.Contains("[ERROR]") || l.Contains("[CRITICAL]")).ToList();
        if (errorLines.Count > 0)
        {
            findings.Add(new TroubleshootFindingDto(
                errorLines.Count >= 3 ? TroubleshootSeverity.Critical : TroubleshootSeverity.Warning,
                $"{errorLines.Count} errores en los registros",
                "Último error: " + Trim(errorLines[^1], 260),
                "Abra Logs\\server-*.log en el servidor y corrija la causa antes de reiniciar el servicio."));
        }

        if (stations.Count == 0)
        {
            findings.Add(new TroubleshootFindingDto(
                TroubleshootSeverity.Info,
                "No hay equipos registrados",
                "El servidor no tiene estaciones activas.",
                "Use «Escanear la red» en el panel de equipos para dar de alta las estaciones."));
        }

        var logExcerpts = request.IncludeLogs
            ? logs.Where(l => l.Contains("[ERROR]") || l.Contains("[CRITICAL]") || l.Contains("[WARN]"))
                  .TakeLast(12).ToList()
            : new List<string>();
        if (logExcerpts.Count == 0 && request.IncludeLogs)
            logExcerpts = logs.TakeLast(8).ToList();

        var summary = findings.Count == 0
            ? "No se detectaron incidencias con la información disponible."
            : $"{findings.Count(f => f.Severity == TroubleshootSeverity.Critical)} críticos, " +
              $"{findings.Count(f => f.Severity == TroubleshootSeverity.Warning)} advertencias y " +
              $"{findings.Count(f => f.Severity == TroubleshootSeverity.Info)} informativos.";

        var aiConfigured = IsAiEnabled() && !string.IsNullOrWhiteSpace(ResolveApiKey());
        var (aiUsed, explanation, aiError) = request.UseAi
            ? await ExplainAsync(BuildPrompt(findings, stations, offline, activeSessions.Count,
                updateStatus, logExcerpts, logs, onlineCount), ct)
            : (false, "Análisis de IA omitido: se ejecutaron solo las reglas.", null);

        _log.LogInformation("Diagnóstico ejecutado: {Findings} hallazgos, IA={AiUsed}, error IA={AiError}",
            findings.Count, aiUsed, aiError);

        return new TroubleshootReportDto(
            now,
            AppInfo.Version,
            stations.Count,
            onlineCount,
            aiConfigured,
            aiUsed,
            aiError,
            summary,
            explanation,
            findings,
            logExcerpts);
    }

    private string BuildPrompt(
        List<TroubleshootFindingDto> findings,
        List<Station> stations,
        List<Station> offline,
        int activeSessionCount,
        ClientUpdateStatusDto updateStatus,
        List<string> logExcerpts,
        List<string> allLogs,
        int onlineCount)
    {
        var hardwareLines = allLogs
            .Where(l => l.Contains("Diagnóstico de equipo", StringComparison.OrdinalIgnoreCase) ||
                        l.Contains("Hardware", StringComparison.OrdinalIgnoreCase))
            .TakeLast(5)
            .ToList();

        var snapshot = new
        {
            servidor = new
            {
                version = AppInfo.Version,
                equiposActivos = stations.Count,
                equiposEnLinea = onlineCount,
                equiposSinLatido = offline.Count,
                agentesConectados = stations.Count(s => AgentHub.IsConnected(s.Id))
            },
            cliente = new
            {
                versionPaquete = updateStatus.PackageVersion,
                tienePaquete = updateStatus.HasPackage,
                alDia = updateStatus.UpToDate,
                desactualizados = updateStatus.Outdated,
                sinVersion = updateStatus.Unknown
            },
            equipos = offline.Take(20).Select(s => new
            {
                id = s.Id,
                nombre = s.Name,
                estado = s.Status.ToString(),
                version = s.Version,
                ultimoLatidoUtc = s.LastSeenUtc,
                tieneMac = !string.IsNullOrWhiteSpace(s.MacAddress),
                agenteConectado = AgentHub.IsConnected(s.Id)
            }),
            sesionesActivas = activeSessionCount,
            hardware = hardwareLines,
            hallazgos = findings.Select(f => new
            {
                gravedad = f.Severity.ToString(),
                titulo = f.Title,
                detalle = f.Detail
            }),
            registros = logExcerpts
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        return "Eres un técnico de soporte de CyberManager. El entorno es Windows: el servidor es la " +
               "tarea programada «CyberManagerServer», la consola es WPF y cada estación ejecuta el " +
               "servicio de Windows «CyberManagerAgent». Analiza el siguiente diagnóstico y responde en " +
               "español, en máximo 12 líneas, sin inventar datos. Indica la causa más probable, los pasos " +
               "concretos de verificación y una recomendación priorizando la seguridad de los cobros y la " +
               "disponibilidad de los equipos. Usa exclusivamente herramientas de Windows: " +
               "`Get-Service CyberManagerAgent`, `sc.exe query CyberManagerAgent`, el Visor de eventos de " +
               "Windows, `Test-NetConnection <IP> -Port 5080` y la consola de CyberManager para publicar " +
               "actualizaciones o reparar el agente. No menciones systemctl, /var/log ni comandos de Linux. " +
               "No propongas borrar datos ni desactivar la autenticación. Datos (tokens, correos, RIF, IPs y " +
               "números largos ya están sanitizados):\n" +
               json;
    }

    private bool IsAiEnabled()
        => _config.GetValue<bool?>("Troubleshoot:Ai:Enabled") ?? true;

    private string? ResolveApiKey()
    {
        var envName = _config.GetValue<string>("Troubleshoot:Ai:ApiKeyEnvironmentVariable")
                      ?? "CYBERMANAGER_AI_API_KEY";
        var fromEnvironment = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment.Trim();
        return _config.GetValue<string>("Troubleshoot:Ai:ApiKey")?.Trim();
    }

    private async Task<(bool Used, string Explanation, string? Error)> ExplainAsync(
        string prompt, CancellationToken ct)
    {
        if (!IsAiEnabled())
            return (false, "La IA en la nube está desactivada en la configuración del servidor.", null);

        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false,
                "Configure la variable de entorno CYBERMANAGER_AI_API_KEY (o Troubleshoot:Ai:ApiKey) " +
                "para habilitar la explicación con IA en la nube.",
                "No hay API key configurada.");

        var baseUrl = _config.GetValue<string>("Troubleshoot:Ai:BaseUrl")
                      ?? "https://api.openai.com/v1";
        var model = _config.GetValue<string>("Troubleshoot:Ai:Model") ?? "gpt-4o-mini";
        var timeoutSeconds = _config.GetValue<int?>("Troubleshoot:Ai:TimeoutSeconds") ?? 20;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var client = _http.CreateClient();
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            var payload = new
            {
                model,
                temperature = 0.2,
                max_tokens = 500,
                messages = new object[]
                {
                    new { role = "system", content = "Responde siempre en español y de forma concisa." },
                    new { role = "user", content = prompt }
                }
            };

            using var response = await client.PostAsJsonAsync("chat/completions", payload, linked.Token);
            var body = await response.Content.ReadAsStringAsync(linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                return (false,
                    "El proveedor de IA no pudo generar la explicación; se muestran solo los hallazgos de reglas.",
                    $"HTTP {(int)response.StatusCode}: {Sanitize(Trim(body, 300))}");
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return (true, Sanitize(content ?? string.Empty), null);
        }
        catch (OperationCanceledException)
        {
            return (false,
                "La IA en la nube tardó demasiado en responder; se muestran solo los hallazgos de reglas.",
                $"Tiempo de espera agotado ({timeoutSeconds} s).");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo obtener la explicación de IA en la nube.");
            return (false,
                "No se pudo conectar con el proveedor de IA; se muestran solo los hallazgos de reglas.",
                Sanitize(Trim(ex.Message, 300)));
        }
    }

    private async Task<List<string>> ReadRecentLogsAsync(int maxLines, CancellationToken ct)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Logs");
        if (!Directory.Exists(folder)) return new List<string>();

        var files = Directory.EnumerateFiles(folder, "server-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(3);

        var lines = new Queue<string>(Math.Max(maxLines, 1));
        foreach (var file in files)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, true);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (lines.Count >= maxLines) lines.Dequeue();
                    lines.Enqueue(Sanitize(Trim(line, 700)));
                }

                if (lines.Count >= maxLines) break;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "No se pudo leer {LogFile} para el diagnóstico.", file);
            }
        }

        return lines.ToList();
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var text = BearerRegex.Replace(value, "Bearer [REDACTED]");
        text = JwtRegex.Replace(text, "[TOKEN]");
        text = EmailRegex.Replace(text, "[EMAIL]");
        text = RifRegex.Replace(text, "[RIF]");
        text = IpRegex.Replace(text, "[IP]");
        text = LongNumberRegex.Replace(text, "[NUMBER]");
        return text;
    }

    private static string Trim(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
