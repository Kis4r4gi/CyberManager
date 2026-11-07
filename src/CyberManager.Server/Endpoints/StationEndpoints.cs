using System.Security.Claims;
using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

public static class StationEndpoints
{
    public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stations").WithTags("Equipos").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db, bool includeInactive = false) =>
        {
            var q = db.Stations.AsNoTracking().Include(s => s.Zone).Include(s => s.Rate).AsQueryable();
            if (!includeInactive) q = q.Where(s => s.Active);
            var list = await q.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync();
            return Results.Ok(list.Select(Mappers.ToDto).ToList());
        });

        g.MapPost("/", async (SaveStationRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("El nombre es obligatorio.");
            if (await db.Stations.AnyAsync(s => s.Name == req.Name))
                throw new AppException("Ya existe un equipo con ese nombre.", 409);

            var st = new Station
            {
                Name = req.Name, IpAddress = req.IpAddress, MacAddress = req.MacAddress,
                ZoneId = req.ZoneId, RateId = req.RateId, Notes = req.Notes,
                SortOrder = req.SortOrder, Active = req.Active,
                Kind = req.Kind, Platform = req.Platform, Controllers = req.Controllers,
                DeviceToken = Guid.NewGuid().ToString("N"),
                Status = StationStatus.Offline,
                IsLocked = true
            };
            db.Stations.Add(st);
            await db.SaveChangesAsync();
            var full = await db.Stations.AsNoTracking().Include(s => s.Zone).Include(s => s.Rate)
                .FirstAsync(s => s.Id == st.Id);
            return Results.Ok(Mappers.ToDto(full));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveStationRequest req, AppDbContext db) =>
        {
            var st = await db.Stations.Include(s => s.Zone).Include(s => s.Rate)
                .FirstOrDefaultAsync(s => s.Id == id) ?? throw AppException.NotFound("Equipo");
            st.Name = req.Name; st.IpAddress = req.IpAddress; st.MacAddress = req.MacAddress;
            st.ZoneId = req.ZoneId; st.RateId = req.RateId; st.Notes = req.Notes;
            st.SortOrder = req.SortOrder; st.Active = req.Active;
            st.Kind = req.Kind; st.Platform = req.Platform; st.Controllers = req.Controllers;
            await db.SaveChangesAsync();

            st = await db.Stations.AsNoTracking().Include(s => s.Zone).Include(s => s.Rate)
                .FirstAsync(s => s.Id == id);
            return Results.Ok(Mappers.ToDto(st));
        }).RequireAuthorization("Admin");

        g.MapPost("/reorder", async (ReorderStationsRequest req, AppDbContext db) =>
        {
            var ids = (req.StationIds ?? new List<int>()).Distinct().ToList();
            if (ids.Count == 0)
                throw new AppException("Indique el orden de los equipos.");

            var stations = await db.Stations.Where(s => ids.Contains(s.Id)).ToListAsync();
            if (stations.Count != ids.Count)
                throw new AppException("Uno o más equipos ya no existen.", 404);

            for (var index = 0; index < ids.Count; index++)
            {
                var station = stations.First(s => s.Id == ids[index]);
                station.SortOrder = index + 1;
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var st = await db.Stations.FirstOrDefaultAsync(s => s.Id == id) ?? throw AppException.NotFound("Equipo");
            if (await db.Sessions.AnyAsync(s => s.StationId == id
                    && s.Status != SessionStatus.Closed && s.Status != SessionStatus.Cancelled))
                throw new AppException("El equipo tiene una sesión activa.");
            st.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");

        g.MapPost("/{id:int}/token", async (int id, AppDbContext db) =>
        {
            var st = await db.Stations.FirstOrDefaultAsync(s => s.Id == id) ?? throw AppException.NotFound("Equipo");
            st.DeviceToken = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            return Results.Ok(new { stationId = st.Id, token = st.DeviceToken });
        }).RequireAuthorization("Admin");

        g.MapGet("/{id:int}/config", async (int id, AppDbContext db) =>
        {
            var st = await db.Stations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id)
                     ?? throw AppException.NotFound("Equipo");
            return Results.Ok(new
            {
                stationId = st.Id,
                name = st.Name,
                token = st.DeviceToken,
                serverUrl = "http://localhost:5080"
            });
        }).RequireAuthorization("Admin");

        g.MapPost("/{id:int}/command", async (int id, StationCommandRequest req,
            AppDbContext db, RealtimeService realtime) =>
        {
            var st = await db.Stations.FirstOrDefaultAsync(s => s.Id == id) ?? throw AppException.NotFound("Equipo");

            if (req.Command == AgentCommandType.Lock) st.IsLocked = true;
            if (req.Command == AgentCommandType.Unlock) st.IsLocked = false;
            await db.SaveChangesAsync();

            if (req.Command is AgentCommandType.Shutdown or AgentCommandType.Restart
                or AgentCommandType.Logoff or AgentCommandType.Screenshot)
            {
                // Solo el agente (servicio de Windows) puede apagar, reiniciar o cerrar sesión.
                await realtime.SendAgentCommandAsync(id, req.Command, req.Argument);
            }
            else if (req.Command is AgentCommandType.SetVolume or AgentCommandType.Mute
                     or AgentCommandType.Unmute or AgentCommandType.LockInput
                     or AgentCommandType.UnlockInput or AgentCommandType.MonitorOff
                     or AgentCommandType.MonitorOn)
            {
                // Audio, monitor y bloqueo de entrada se ejecutan en la sesión interactiva
                // del cliente, donde vive la pantalla del equipo.
                await realtime.SendStationCommandAsync(id, req.Command, req.Argument);
            }
            else
            {
                await Task.WhenAll(
                    realtime.SendAgentCommandAsync(id, req.Command, req.Argument),
                    realtime.SendStationCommandAsync(id, req.Command, req.Argument));
            }

            await realtime.BroadcastEventAsync("CommandSent", $"{st.Name}: {req.Command}");
            return Results.Ok(new { sent = true });
        });

        g.MapPost("/{id:int}/maintenance", async (int id, bool enabled, AppDbContext db,
            RealtimeService realtime) =>
        {
            var st = await db.Stations.Include(s => s.Zone).Include(s => s.Rate)
                .FirstOrDefaultAsync(s => s.Id == id) ?? throw AppException.NotFound("Equipo");
            if (enabled && st.CurrentSessionId is not null)
                throw new AppException("Cierre la sesión antes de poner el equipo en mantenimiento.");

            st.Status = enabled ? StationStatus.Maintenance : StationStatus.Available;
            if (enabled) st.IsLocked = true;
            await db.SaveChangesAsync();
            await realtime.BroadcastEventAsync("StationMaintenance", $"{st.Name}: mantenimiento {(enabled ? "activado" : "desactivado")}");
            return Results.Ok(Mappers.ToDto(st));
        });

        app.MapGet("/api/dashboard", async (DashboardService dashboard) => Results.Ok(await dashboard.BuildAsync()))
            .RequireAuthorization();

        return app;
    }
}
