using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Endpoints;

/// <summary>
/// Autoservicio prepago desde la pantalla de bloqueo: crear cuenta, iniciar sesión,
/// comprar paquetes de tiempo y abrir/cerrar la sesión del equipo.
/// </summary>
public static class PrepaidEndpoints
{
    public static IEndpointRouteBuilder MapPrepaidEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/station/prepaid").WithTags("Prepago").AllowAnonymous();

        g.MapGet("/info", async (PrepaidService prepaid) =>
            Results.Ok(await prepaid.InfoAsync()));

        g.MapPost("/register", async (HttpContext http, PrepaidRegisterRequest req,
            AppDbContext db, PrepaidService prepaid) =>
        {
            await StationClientEndpoints.RequireStationAsync(http, db);
            return Results.Ok(await prepaid.RegisterAsync(req));
        });

        g.MapPost("/login", async (HttpContext http, PrepaidLoginRequest req,
            AppDbContext db, PrepaidService prepaid) =>
        {
            var station = await StationClientEndpoints.RequireStationAsync(http, db);
            return Results.Ok(await prepaid.LoginAsync(station.Id, req));
        });

        g.MapPost("/recharge", async (HttpContext http, PrepaidRechargeRequest req,
            AppDbContext db, PrepaidService prepaid) =>
        {
            var station = await StationClientEndpoints.RequireStationAsync(http, db);
            return Results.Ok(await prepaid.RechargeAsync(station.Id, req));
        });

        g.MapPost("/start", async (HttpContext http, PrepaidStartRequest req,
            AppDbContext db, PrepaidService prepaid) =>
        {
            var station = await StationClientEndpoints.RequireStationAsync(http, db);
            return Results.Ok(await prepaid.StartAsync(station.Id, req));
        });

        g.MapPost("/finish", async (HttpContext http, AppDbContext db, PrepaidService prepaid) =>
        {
            var station = await StationClientEndpoints.RequireStationAsync(http, db);
            return Results.Ok(await prepaid.FinishAsync(station.Id));
        });

        return app;
    }
}
