using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Hubs;

/// <summary>Agente Windows de cada equipo. Se autentica con el token del equipo.</summary>
public class AgentHub : Hub
{
    private readonly AppDbContext _db;
    private readonly RealtimeService _realtime;

    public AgentHub(AppDbContext db, RealtimeService realtime)
    {
        _db = db;
        _realtime = realtime;
    }

    public override async Task OnConnectedAsync()
    {
        var stationId = await AuthenticateAsync();
        if (stationId is null)
        {
            Context.Abort();
            return;
        }

        Context.Items["stationId"] = stationId.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeService.AgentGroup(stationId.Value));
        await Clients.Caller.SendAsync("Ready", stationId.Value);
        await _realtime.BroadcastEventAsync("AgentOnline", $"Equipo {stationId} conectado");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("stationId", out var v) && v is int id)
            await _realtime.BroadcastEventAsync("AgentOffline", $"Equipo {id} desconectado");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task Heartbeat(AgentHeartbeat hb)
    {
        if (Context.Items.TryGetValue("stationId", out var v) && v is int id && id != hb.StationId) return;

        var station = await _db.Stations.FirstOrDefaultAsync(s => s.Id == hb.StationId);
        if (station is null) return;

        var wasOffline = station.Status == StationStatus.Offline;
        station.LastSeenUtc = DateTime.UtcNow;
        station.Version = hb.Version;
        station.CurrentUser = hb.CurrentUser;
        station.IsLocked = string.Equals(hb.AgentState, "locked", StringComparison.OrdinalIgnoreCase);

        // Un equipo conectado sin sesión deja de estar fuera de línea.
        if (wasOffline && station.CurrentSessionId is null)
            station.Status = StationStatus.Available;

        await _db.SaveChangesAsync();

        if (wasOffline && station.Status == StationStatus.Available)
        {
            await _realtime.BroadcastEventAsync("StationOnline", $"{station.Name} en línea");
            await _realtime.StationChangedAsync(new StationStateEvent(
                station.Id, station.Status, null, 0, 0, 0, false));
        }
    }

    public Task Report(CommandResult result)
        => _realtime.BroadcastEventAsync("CommandResult", result.Message, result);

    private async Task<int?> AuthenticateAsync()
    {
        var http = Context.GetHttpContext();
        var idText = http?.Request.Query["stationId"].ToString();
        var token = http?.Request.Query["token"].ToString();
        if (!int.TryParse(idText, out var id) || string.IsNullOrWhiteSpace(token)) return null;

        var station = await _db.Stations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (station is null || string.IsNullOrWhiteSpace(station.DeviceToken)) return null;
        return station.DeviceToken == token ? id : null;
    }
}
