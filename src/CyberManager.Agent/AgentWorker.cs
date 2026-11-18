using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CyberManager.Agent;

public class AgentWorker : BackgroundService
{
    private static readonly HttpClient FilterHttp = new() { Timeout = TimeSpan.FromSeconds(12) };

    private readonly AgentConfigStore _config;
    private readonly ClientUpdateApplier _updates;
    private readonly ILogger<AgentWorker> _log;
    private readonly WebFilterService _filter;
    private readonly SystemMetrics _metrics = new();
    private HubConnection? _hub;
    private string _state = "locked";
    private DateTime _lastFilterUpdate = DateTime.MinValue;
    private DateTime _lastLocateAttempt = DateTime.MinValue;

    public AgentWorker(AgentConfigStore config, ClientUpdateApplier updates,
        ILogger<AgentWorker> log, ILogger<WebFilterService> filterLog)
    {
        _config = config;
        _updates = updates;
        _log = log;
        _filter = new WebFilterService(filterLog);
        _config.Changed += OnConfigurationChanged;
    }

    private AgentOptions Options => _config.Current;

    /// <summary>Cuando el escaneo de red asigna un token, el agente reconecta de inmediato.</summary>
    private void OnConfigurationChanged() => _ = ReconnectAsync();

    private async Task ReconnectAsync()
    {
        var hub = _hub;
        _hub = null;
        if (hub is not null)
        {
            try { await hub.DisposeAsync(); } catch { /* cerrando */ }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.IsConfigured)
        {
            _log.LogWarning("Agente sin configurar. Se esperará al escaneo de red del servidor " +
                            "o a la configuración manual con StationId y Token.");
        }
        else
        {
            _log.LogInformation("Agente iniciado para el equipo {StationId} -> {Server}",
                Options.StationId, Options.ServerUrl);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = Options;

            if (options.IsConfigured)
            {
                try
                {
                    if (_hub is null || _hub.State == HubConnectionState.Disconnected)
                        await ConnectAsync(options, stoppingToken);

                    if (_hub is { State: HubConnectionState.Connected })
                        await SendHeartbeatAsync(options, stoppingToken);

                    if (DateTime.UtcNow - _lastFilterUpdate > TimeSpan.FromSeconds(60))
                    {
                        _lastFilterUpdate = DateTime.UtcNow;
                        await UpdateWebFilterAsync(options, stoppingToken);
                    }

                    await _updates.CheckAsync(options, stoppingToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudo contactar al servidor");
                    await TryRelocateAsync(options, stoppingToken);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, options.HeartbeatSeconds)), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _config.Changed -= OnConfigurationChanged;

        if (_hub is not null)
            await _hub.DisposeAsync();

        _filter.Dispose();
    }

    /// <summary>
    /// Si el servidor no responde en la dirección guardada, se busca en la red local
    /// (UDP 5094 y, si hace falta, recorrido de la subred) y se guarda la nueva.
    /// </summary>
    private async Task TryRelocateAsync(AgentOptions options, CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastLocateAttempt < TimeSpan.FromSeconds(30)) return;
        _lastLocateAttempt = DateTime.UtcNow;

        try
        {
            var identity = new DiscoveryIdentity("agent", options.StationId, options.Token,
                Environment.MachineName, NetworkIdentity.LocalIPv4(), NetworkIdentity.PrimaryMac(),
                AppInfo.Version);
            var found = await ServerLocator.FindWorkingAsync(options.ServerUrl,
                TimeSpan.FromSeconds(2), ct, identity);
            if (found is null) return;

            if (string.Equals(ServerLocator.Normalize(found),
                    ServerLocator.Normalize(options.ServerUrl), StringComparison.OrdinalIgnoreCase))
                return;

            _log.LogInformation("Servidor encontrado en {Url}; se actualiza la configuración del agente",
                found);
            _config.Save(options.StationId, options.Token, found);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "No se pudo buscar el servidor en la red");
        }
    }

    private async Task ConnectAsync(AgentOptions options, CancellationToken ct)
    {
        var url = $"{options.ServerUrl.TrimEnd('/')}/hubs/agent" +
                  $"?stationId={options.StationId}&token={Uri.EscapeDataString(options.Token)}";

        _hub = new HubConnectionBuilder()
            .WithUrl(url)
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _hub.On<AgentCommand>("Command", command => HandleCommandAsync(command));
        _hub.On<WebFilterPlaylistDto>("WebFilterChanged", playlist =>
        {
            _filter.Apply(playlist);
            return Task.CompletedTask;
        });
        _hub.On<ClientUpdateManifestDto>("ClientUpdateAvailable", manifest =>
            _updates.ApplyAsync(Options, manifest, CancellationToken.None));
        _hub.Reconnected += _ => { _log.LogInformation("Reconectado al servidor"); return Task.CompletedTask; };
        _hub.Closed += ex =>
        {
            if (ex is not null) _log.LogWarning(ex, "Conexión cerrada");
            return Task.CompletedTask;
        };

        await _hub.StartAsync(ct);
        _log.LogInformation("Conectado al servidor como equipo {StationId}", options.StationId);

        // Avisa de inmediato si el servidor ya tiene un paquete de cliente más nuevo.
        await _updates.CheckNowAsync(options, ct);
    }

    /// <summary>Descarga la lista de NetFiltroWeb y la aplica en el equipo.</summary>
    private async Task UpdateWebFilterAsync(AgentOptions options, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{options.ServerUrl.TrimEnd('/')}/api/station/webfilter");
            request.Headers.Add("X-Station-Id", options.StationId.ToString());
            request.Headers.Add("X-Station-Token", options.Token);

            using var response = await FilterHttp.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return;

            var playlist = await response.Content.ReadFromJsonAsync<WebFilterPlaylistDto>(ct);
            if (playlist is not null) _filter.Apply(playlist);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "NetFiltroWeb: no se pudo descargar la lista");
        }
    }

    private async Task SendHeartbeatAsync(AgentOptions options, CancellationToken ct)
    {
        var heartbeat = new AgentHeartbeat(
            options.StationId,
            Environment.MachineName,
            _metrics.CurrentUser(),
            AppInfo.Version,
            Environment.OSVersion.VersionString,
            _metrics.CpuPercent(),
            _metrics.RamPercent(),
            _state);

        await _hub!.InvokeAsync("Heartbeat", heartbeat, ct);
    }

    private async Task HandleCommandAsync(AgentCommand command)
    {
        _log.LogInformation("Comando recibido: {Command} {Argument}", command.Command, command.Argument);
        var success = true;
        string? message = null;

        try
        {
            switch (command.Command)
            {
                case AgentCommandType.Lock:
                    _state = "locked";
                    message = "Equipo bloqueado";
                    break;

                case AgentCommandType.Unlock:
                    _state = "unlocked";
                    message = "Equipo desbloqueado";
                    break;

                case AgentCommandType.Shutdown:
                    success = WindowsPower.Shutdown(false);
                    message = success ? "Apagando equipo" : "No se pudo apagar";
                    break;

                case AgentCommandType.Restart:
                    success = WindowsPower.Shutdown(true);
                    message = success ? "Reiniciando equipo" : "No se pudo reiniciar";
                    break;

                case AgentCommandType.Logoff:
                    success = WindowsPower.Logoff();
                    message = success ? "Cerrando sesión" : "No se pudo cerrar sesión";
                    break;

                case AgentCommandType.KillProcess:
                    success = KillProcess(command.Argument);
                    message = success ? $"Proceso '{command.Argument}' terminado" : "Proceso no encontrado";
                    break;

                case AgentCommandType.Message:
                    success = SendMessage(command.Argument);
                    message = success ? "Mensaje enviado" : "No se pudo mostrar el mensaje";
                    break;

                case AgentCommandType.Screenshot:
                    success = false;
                    message = "Captura de pantalla no disponible en modo servicio";
                    break;

                case AgentCommandType.SetKiosk:
                    message = "Modo kiosco gestionado por la estación";
                    break;

                default:
                    success = false;
                    message = "Comando no soportado";
                    break;
            }
        }
        catch (Exception ex)
        {
            success = false;
            message = ex.Message;
            _log.LogError(ex, "Error ejecutando comando {Command}", command.Command);
        }

        if (_hub is { State: HubConnectionState.Connected })
            await _hub.InvokeAsync("Report", new CommandResult(command.CommandId, success, message));
    }

    private static bool KillProcess(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var bare = name.Replace(".exe", string.Empty, StringComparison.OrdinalIgnoreCase);
        var processes = Process.GetProcessesByName(bare);
        if (processes.Length == 0) return false;
        foreach (var p in processes)
        {
            try { p.Kill(true); } catch { /* ignorado */ }
        }
        return true;
    }

    private static bool SendMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            var psi = new ProcessStartInfo("msg.exe", $"* /TIME:30 \"{text.Replace("\"", "'")}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
