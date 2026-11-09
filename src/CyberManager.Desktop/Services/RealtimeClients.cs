using Microsoft.AspNetCore.SignalR.Client;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Desktop.Services;

/// <summary>Conexión en tiempo real de la consola de administración.</summary>
public sealed class AdminRealtime : IAsyncDisposable
{
    private HubConnection? _hub;

    public event Action<StationStateEvent>? StationStateChanged;
    public event Action<SessionWarningEvent>? SessionWarning;
    public event Action<RealtimeEvent>? EventReceived;
    public event Action<ScreenFrameResult>? ScreenFrameReceived;
    public event Action<UpdateInfoDto>? UpdateAvailable;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string baseUrl, string token)
    {
        await DisposeAsync();

        _hub = new HubConnectionBuilder()
            .WithUrl($"{baseUrl.TrimEnd('/')}/hubs/admin", options =>
                options.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _hub.On<StationStateEvent>("StationState", e => StationStateChanged?.Invoke(e));
        _hub.On<SessionWarningEvent>("SessionWarning", e => SessionWarning?.Invoke(e));
        _hub.On<RealtimeEvent>("Event", e => EventReceived?.Invoke(e));
        _hub.On<ScreenFrameResult>("ScreenFrame", e => ScreenFrameReceived?.Invoke(e));
        _hub.On<UpdateInfoDto>("UpdateAvailable", e => UpdateAvailable?.Invoke(e));

        _hub.Reconnecting += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };
        _hub.Reconnected += _ => { ConnectionChanged?.Invoke(true); return Task.CompletedTask; };
        _hub.Closed += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };

        await _hub.StartAsync();
        ConnectionChanged?.Invoke(true);
    }

    public Task SendCommandAsync(int stationId, AgentCommandType command, string? argument = null)
        => _hub is { State: HubConnectionState.Connected }
            ? _hub.InvokeAsync("SendCommand", stationId, command, argument)
            : Task.CompletedTask;

    /// <summary>Solicita una captura de la pantalla del equipo indicado.</summary>
    public Task RequestScreenAsync(int stationId, int maxWidth = 1280, int quality = 60)
        => _hub is { State: HubConnectionState.Connected }
            ? _hub.InvokeAsync("RequestScreen", stationId, maxWidth, quality)
            : Task.CompletedTask;

    /// <summary>Envía un evento de mouse o teclado a la estación (control remoto).</summary>
    public Task SendRemoteInputAsync(RemoteInputEvent input)
        => _hub is { State: HubConnectionState.Connected }
            ? _hub.InvokeAsync("SendRemoteInput", input.StationId, input)
            : Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}

/// <summary>Conexión de la pantalla de la estación de trabajo.</summary>
public sealed class StationRealtime : IAsyncDisposable
{
    private HubConnection? _hub;

    public event Action<AgentCommand>? CommandReceived;
    public event Action<StationStateEvent>? StationStateChanged;
    public event Action<SessionWarningEvent>? SessionWarning;
    public event Action<WallpaperPlaylistDto>? WallpaperChanged;
    public event Action<PromoVideoPlaylistDto>? PromoVideosChanged;
    public event Action<ScreenFrameRequest>? ScreenFrameRequested;
    public event Action<RemoteInputEvent>? RemoteInputReceived;
    public event Action<WebFilterPlaylistDto>? WebFilterChanged;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string baseUrl, int stationId, string token)
    {
        await DisposeAsync();

        var url = $"{baseUrl.TrimEnd('/')}/hubs/station" +
                  $"?stationId={stationId}&token={Uri.EscapeDataString(token)}";

        _hub = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _hub.On<AgentCommand>("Command", c => CommandReceived?.Invoke(c));
        _hub.On<StationStateEvent>("StationState", e => StationStateChanged?.Invoke(e));
        _hub.On<SessionWarningEvent>("SessionWarning", e => SessionWarning?.Invoke(e));
        _hub.On<WallpaperPlaylistDto>("WallpaperChanged", p => WallpaperChanged?.Invoke(p));
        _hub.On<PromoVideoPlaylistDto>("PromoVideosChanged", p => PromoVideosChanged?.Invoke(p));
        _hub.On<ScreenFrameRequest>("CaptureScreen", r => ScreenFrameRequested?.Invoke(r));
        _hub.On<RemoteInputEvent>("RemoteInput", e => RemoteInputReceived?.Invoke(e));
        _hub.On<WebFilterPlaylistDto>("WebFilterChanged", p => WebFilterChanged?.Invoke(p));

        _hub.Reconnecting += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };
        _hub.Reconnected += _ => { ConnectionChanged?.Invoke(true); return Task.CompletedTask; };
        _hub.Closed += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };

        await _hub.StartAsync();
        ConnectionChanged?.Invoke(true);
    }

    public Task HeartbeatAsync(AgentHeartbeat heartbeat)
        => _hub is { State: HubConnectionState.Connected }
            ? _hub.InvokeAsync("Heartbeat", heartbeat)
            : Task.CompletedTask;

    /// <summary>Envía al servidor la captura de pantalla solicitada por administración.</summary>
    public Task SendScreenFrameAsync(ScreenFrameResult frame)
        => _hub is { State: HubConnectionState.Connected }
            ? _hub.InvokeAsync("SendScreenFrame", frame)
            : Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}
