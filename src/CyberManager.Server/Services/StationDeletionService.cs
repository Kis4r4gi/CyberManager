using CyberManager.Server.Data;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>
/// Eliminación permanente de una estación. Quita también sesiones, ventas, reservas,
/// registros de agente y auditoría asociados, y guarda la identidad (MAC/IP) para que
/// el descubrimiento automático no vuelva a dar de alta ese mismo PC por error.
/// </summary>
public sealed class StationDeletionService(AppDbContext db, RealtimeService realtime)
{
    public async Task<StationDeleteResult> DeleteAsync(int id, CancellationToken ct)
    {
        var station = await db.Stations.FirstOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw AppException.NotFound("Equipo");

        if (await db.Sessions.AnyAsync(s => s.StationId == id
                && s.Status != SessionStatus.Closed && s.Status != SessionStatus.Cancelled, ct))
            throw new AppException("El equipo tiene una sesión activa.");

        var sessionIds = await db.Sessions.Where(s => s.StationId == id)
            .Select(s => s.Id).ToListAsync(ct);

        var saleIds = await db.Sales
            .Where(s => s.StationId == id ||
                        (s.SessionId != null && sessionIds.Contains(s.SessionId.Value)))
            .Select(s => s.Id).ToListAsync(ct);

        if (saleIds.Count > 0)
        {
            var items = await db.SaleItems.Where(i => saleIds.Contains(i.SaleId)).ToListAsync(ct);
            db.SaleItems.RemoveRange(items);
            var sales = await db.Sales.Where(s => saleIds.Contains(s.Id)).ToListAsync(ct);
            db.Sales.RemoveRange(sales);
        }

        var sessions = await db.Sessions.Where(s => s.StationId == id).ToListAsync(ct);
        db.Sessions.RemoveRange(sessions);

        var reservations = await db.Reservations.Where(r => r.StationId == id).ToListAsync(ct);
        db.Reservations.RemoveRange(reservations);

        var devices = await db.Devices.Where(d => d.StationId == id).ToListAsync(ct);
        db.Devices.RemoveRange(devices);

        var entityId = id.ToString();
        var audits = await db.AuditLogs
            .Where(a => a.Entity == "Station" && a.EntityId == entityId)
            .ToListAsync(ct);
        db.AuditLogs.RemoveRange(audits);

        if (!string.IsNullOrWhiteSpace(station.MacAddress) ||
            !string.IsNullOrWhiteSpace(station.IpAddress))
        {
            db.DeletedStationIdentities.Add(new DeletedStationIdentity
            {
                MacAddress = station.MacAddress,
                IpAddress = station.IpAddress,
                MachineName = station.Name,
                DeletedUtc = DateTime.UtcNow
            });
        }

        db.Stations.Remove(station);
        await db.SaveChangesAsync(ct);

        await realtime.BroadcastEventAsync("StationsChanged", $"Equipo eliminado: {station.Name}");

        return new StationDeleteResult(id, sessions.Count, saleIds.Count, reservations.Count, devices.Count);
    }
}
