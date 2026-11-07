using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Hubs;

/// <summary>Pantalla de la estación de trabajo (bloqueo, tiempo restante, consumos).</summary>
public class StationHub : Hub
{
    private readonly AppDbContext _db;
    private readonly RealtimeService _realtime;

    public StationHub(AppDbContext db, RealtimeService realtime)
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
        await Groups.AddToGroupAsync(Context.ConnectionId, $"station-{stationId.Value}");
        await Clients.Caller.SendAsync("Ready", stationId.Value);
        await base.OnConnectedAsync();
    }

    public async Task Heartbeat(AgentHeartbeat hb)
    {
        if (Context.Items.TryGetValue("stationId", out var v) && v is int id && id != hb.StationId) return;

        var station = await _db.Stations.FirstOrDefaultAsync(s => s.Id == hb.StationId);
        if (station is null) return;

        station.LastSeenUtc = DateTime.UtcNow;
        station.Version = hb.Version;
        station.CurrentUser = hb.CurrentUser;
        station.IsLocked = string.Equals(hb.AgentState, "locked", StringComparison.OrdinalIgnoreCase);
        await _db.SaveChangesAsync();
    }

    public Task Report(CommandResult result)
        => _realtime.BroadcastEventAsync("CommandResult", result.Message, result);

    /// <summary>Recibe la captura de pantalla de la estación y la reenvía a administración.</summary>
    public Task SendScreenFrame(ScreenFrameResult frame)
    {
        if (Context.Items.TryGetValue("stationId", out var value) && value is int stationId &&
            stationId != frame.StationId)
            return Task.CompletedTask;

        return _realtime.ScreenFrameAsync(frame);
    }

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
