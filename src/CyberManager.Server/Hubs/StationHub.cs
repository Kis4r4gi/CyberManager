using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
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

        var wasOffline = station.Status == StationStatus.Offline;
        station.LastSeenUtc = DateTime.UtcNow;
        station.Version = hb.Version;
        station.CurrentUser = hb.CurrentUser;
        station.IsLocked = string.Equals(hb.AgentState, "locked", StringComparison.OrdinalIgnoreCase);

        // Un equipo que vuelve a reportar deja de estar fuera de línea.
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
        var ip = http?.Request.Query["ipAddress"].ToString();
        var mac = http?.Request.Query["macAddress"].ToString();
        if (!int.TryParse(idText, out var id)) return null;

        var station = await _db.Stations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (station is null) return null;

        if (!string.IsNullOrWhiteSpace(token))
            return !string.IsNullOrWhiteSpace(station.DeviceToken) && station.DeviceToken == token
                ? id
                : null;

        // Compatibilidad con equipos configurados solo por IP y MAC.
        var normalizedMac = NormalizeMac(mac);
        if (normalizedMac.Length > 0 &&
            string.Equals(NormalizeMac(station.MacAddress), normalizedMac, StringComparison.Ordinal))
            return id;

        if (!string.IsNullOrWhiteSpace(ip) &&
            string.Equals(station.IpAddress, ip, StringComparison.OrdinalIgnoreCase))
            return id;

        return null;
    }

    private static string NormalizeMac(string? mac)
        => (mac ?? string.Empty).Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();
}
