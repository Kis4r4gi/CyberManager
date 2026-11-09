using System.Diagnostics;
using System.Net.Http.Json;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace CyberManager.Agent;

public class AgentWorker : BackgroundService
{
    private static readonly HttpClient FilterHttp = new() { Timeout = TimeSpan.FromSeconds(12) };

    private readonly AgentOptions _options;
    private readonly ILogger<AgentWorker> _log;
    private readonly WebFilterService _filter;
    private readonly SystemMetrics _metrics = new();
    private HubConnection? _hub;
    private string _state = "locked";
    private DateTime _lastFilterUpdate = DateTime.MinValue;

    public AgentWorker(IOptions<AgentOptions> options, ILogger<AgentWorker> log,
        ILogger<WebFilterService> filterLog)
    {
        _options = options.Value;
        _log = log;
        _filter = new WebFilterService(filterLog);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _log.LogWarning("Agente sin configurar. Edite appsettings.json con StationId y Token " +
                            "(se obtienen desde Administración > Equipos en la consola).");
            return;
        }

        _log.LogInformation("Agente iniciado para el equipo {StationId} -> {Server}",
            _options.StationId, _options.ServerUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_hub is null || _hub.State == HubConnectionState.Disconnected)
                    await ConnectAsync(stoppingToken);

                if (_hub is { State: HubConnectionState.Connected })
                    await SendHeartbeatAsync(stoppingToken);

                if (DateTime.UtcNow - _lastFilterUpdate > TimeSpan.FromSeconds(60))
                {
                    _lastFilterUpdate = DateTime.UtcNow;
                    await UpdateWebFilterAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo contactar al servidor");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, _options.HeartbeatSeconds)), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        if (_hub is not null)
            await _hub.DisposeAsync();

        _filter.Dispose();
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        var url = $"{_options.ServerUrl.TrimEnd('/')}/hubs/agent" +
                  $"?stationId={_options.StationId}&token={Uri.EscapeDataString(_options.Token)}";

        _hub = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _hub.On<AgentCommand>("Command", command => HandleCommandAsync(command));
        _hub.On<WebFilterPlaylistDto>("WebFilterChanged", playlist =>
        {
            _filter.Apply(playlist);
            return Task.CompletedTask;
        });
        _hub.Reconnected += _ => { _log.LogInformation("Reconectado al servidor"); return Task.CompletedTask; };
        _hub.Closed += ex =>
        {
            if (ex is not null) _log.LogWarning(ex, "Conexión cerrada");
            return Task.CompletedTask;
        };

        await _hub.StartAsync(ct);
        _log.LogInformation("Conectado al servidor como equipo {StationId}", _options.StationId);
    }

    /// <summary>Descarga la lista de NetFiltroWeb y la aplica en el equipo.</summary>
    private async Task UpdateWebFilterAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_options.ServerUrl.TrimEnd('/')}/api/station/webfilter");
            request.Headers.Add("X-Station-Id", _options.StationId.ToString());
            request.Headers.Add("X-Station-Token", _options.Token);

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

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        var heartbeat = new AgentHeartbeat(
            _options.StationId,
            Environment.MachineName,
            _metrics.CurrentUser(),
            typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "1.0.0",
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
