using CyberManager.Server.Hubs;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace CyberManager.Server.Services;

public class RealtimeService
{
    private readonly IHubContext<AdminHub> _admin;
    private readonly IHubContext<StationHub> _station;
    private readonly IHubContext<AgentHub> _agent;

    public RealtimeService(IHubContext<AdminHub> admin, IHubContext<StationHub> station, IHubContext<AgentHub> agent)
    {
        _admin = admin;
        _station = station;
        _agent = agent;
    }

    public Task BroadcastEventAsync(string type, string? message = null, object? payload = null)
        => _admin.Clients.All.SendAsync("Event", new RealtimeEvent(type, message, payload));

    public async Task StationChangedAsync(StationStateEvent state)
    {
        await _admin.Clients.All.SendAsync("StationState", state);
        await _station.Clients.Group($"station-{state.StationId}").SendAsync("StationState", state);
    }

    public async Task SessionWarningAsync(SessionWarningEvent warning)
    {
        await _admin.Clients.All.SendAsync("SessionWarning", warning);
        await _station.Clients.Group($"station-{warning.StationId}").SendAsync("SessionWarning", warning);
    }

    /// <summary>Envía la nueva galería a todas las estaciones y a la consola de administración.</summary>
    public async Task WallpaperChangedAsync(WallpaperPlaylistDto playlist)
    {
        await _station.Clients.All.SendAsync("WallpaperChanged", playlist);
        await _admin.Clients.All.SendAsync("Event",
            new RealtimeEvent("WallpaperChanged", "Galería de fondos actualizada", playlist));
    }

    /// <summary>Envía la nueva lista de videos promocionales a todas las estaciones.</summary>
    public async Task PromoVideosChangedAsync(PromoVideoPlaylistDto playlist)
    {
        await _station.Clients.All.SendAsync("PromoVideosChanged", playlist);
        await _admin.Clients.All.SendAsync("Event",
            new RealtimeEvent("PromoVideosChanged", "Videos promocionales actualizados", playlist));
    }

    public Task SendAgentCommandAsync(int stationId, AgentCommandType type, string? argument = null, int? durationSeconds = null)
    {
        var cmd = new AgentCommand(Guid.NewGuid(), type, argument, durationSeconds);
        return _agent.Clients.Group(AgentGroup(stationId)).SendAsync("Command", cmd);
    }

    public Task SendStationCommandAsync(int stationId, AgentCommandType type, string? argument = null, int? durationSeconds = null)
    {
        var cmd = new AgentCommand(Guid.NewGuid(), type, argument, durationSeconds);
        return _station.Clients.Group($"station-{stationId}").SendAsync("Command", cmd);
    }

    /// <summary>Pide a una estación una captura de pantalla para el visualizador remoto.</summary>
    public Task RequestScreenAsync(int stationId, int maxWidth = 1280, int quality = 60)
    {
        var request = new ScreenFrameRequest(stationId, Guid.NewGuid(),
            Math.Clamp(maxWidth, 320, 1920), Math.Clamp(quality, 20, 90));
        return _station.Clients.Group($"station-{stationId}").SendAsync("CaptureScreen", request);
    }

    /// <summary>Reenvía la captura a las consolas de administración conectadas.</summary>
    public Task ScreenFrameAsync(ScreenFrameResult frame)
        => _admin.Clients.All.SendAsync("ScreenFrame", frame);

    public static string AgentGroup(int stationId) => $"agent-{stationId}";
}
