using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

/// <summary>API que consume la pantalla de la estación, autenticada con el token del equipo.</summary>
public static class StationClientEndpoints
{
    public static IEndpointRouteBuilder MapStationClientEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/station").WithTags("Estación").AllowAnonymous();

        g.MapGet("/state", async (HttpContext http, AppDbContext db, SettingsService settings,
            ExchangeRateService rates, WallpaperService wallpapers, PromoVideoService promos) =>
        {
            var station = await RequireStationAsync(http, db);
            var now = DateTime.UtcNow;
            var session = await db.Sessions.AsNoTracking().Include(s => s.Station).Include(s => s.Customer)
                .Where(s => s.StationId == station.Id
                            && s.Status != SessionStatus.Closed && s.Status != SessionStatus.Cancelled)
                .OrderByDescending(s => s.Id).FirstOrDefaultAsync();

            SessionDto? sessionDto = null;
            if (session is not null)
            {
                var consumptionTotals = await db.Sales.AsNoTracking()
                    .Where(s => s.SessionId == session.Id && s.Status == SaleStatus.Completed)
                    .Select(s => s.Total)
                    .ToListAsync();
                var consumption = consumptionTotals.Sum();
                sessionDto = Mappers.ToDto(session, consumption, now);
            }

            var barScale = int.TryParse(await settings.GetAsync(SettingsKeys.SessionBarScale, "100"), out var scale)
                ? Math.Clamp(scale, 70, 160)
                : 100;

            return Results.Ok(new StationClientStateDto(
                station.Id, station.Name, station.Zone?.Name, station.IsLocked, station.Status,
                sessionDto, await rates.GetRateAsync(now),
                await settings.GetAsync(SettingsKeys.BusinessName, "CyberManager"),
                await settings.GetAsync(SettingsKeys.LocalCurrencySymbol, "Bs"),
                await settings.GetAsync(SettingsKeys.TicketFooter),
                now, await wallpapers.PlaylistAsync(), await promos.PlaylistAsync(),
                await settings.GetAsync(SettingsKeys.SessionBarDescription),
                barScale));
        });

        g.MapGet("/webfilter", async (HttpContext http, AppDbContext db, SettingsService settings) =>
        {
            await RequireStationAsync(http, db);
            var rules = await db.WebFilterRules.AsNoTracking()
                .Where(r => r.Active)
                .OrderBy(r => r.Pattern)
                .Select(r => new WebFilterRuleDto(r.Id, r.Pattern, r.Kind, r.Active, r.Notes,
                    r.CreatedUtc, r.UpdatedUtc))
                .ToListAsync();
            var proxyEnabled = await settings.GetBoolAsync(SettingsKeys.WebFilterProxyEnabled, true);
            var proxyPort = await settings.GetIntAsync(SettingsKeys.WebFilterProxyPort, 8877);
            return Results.Ok(new WebFilterPlaylistDto(rules, DateTime.UtcNow, proxyEnabled,
                Math.Clamp(proxyPort, 1024, 65535)));
        });

        g.MapGet("/products", async (HttpContext http, AppDbContext db) =>
        {
            await RequireStationAsync(http, db);
            var products = await db.Products.AsNoTracking().Include(p => p.Category)
                .Where(p => p.Active).OrderBy(p => p.Name).ToListAsync();
            return Results.Ok(products.Select(Mappers.ToDto).ToList());
        });

        g.MapGet("/categories", async (HttpContext http, AppDbContext db) =>
        {
            await RequireStationAsync(http, db);
            return Results.Ok(await db.Categories.AsNoTracking().Where(c => c.Active)
                .OrderBy(c => c.SortOrder)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.Active)).ToListAsync());
        });

        g.MapGet("/wallpapers/{id:int}/file", async (int id, HttpContext http, AppDbContext db) =>
        {
            await RequireStationAsync(http, db);
            var wallpaper = await db.Wallpapers.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id)
                            ?? throw AppException.NotFound("Fondo de pantalla");
            return Results.File(wallpaper.Data, wallpaper.ContentType, enableRangeProcessing: true);
        });

        g.MapGet("/promo-videos/{id:int}/file", async (int id, HttpContext http, AppDbContext db) =>
        {
            await RequireStationAsync(http, db);
            var video = await db.PromoVideos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id)
                        ?? throw AppException.NotFound("Video promocional");
            return Results.File(video.Data, video.ContentType, enableRangeProcessing: true);
        });

        g.MapPost("/order", async (HttpContext http, StationOrderRequest request,
            AppDbContext db, SaleService sales) =>
        {
            var station = await RequireStationAsync(http, db);
            if (request.Items is null || request.Items.Count == 0)
                throw new AppException("No hay productos en el pedido.");

            var session = await db.Sessions.AsNoTracking()
                .Where(s => s.StationId == station.Id
                            && s.Status != SessionStatus.Closed && s.Status != SessionStatus.Cancelled)
                .OrderByDescending(s => s.Id).FirstOrDefaultAsync()
                ?? throw new AppException("El equipo no tiene una sesión activa.", 409);

            var lines = new List<SaleItemRequest>();
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0) continue;
                var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProductId)
                              ?? throw AppException.NotFound("Producto");
                if (!product.Active) throw new AppException($"'{product.Name}' no está disponible.");

                lines.Add(new SaleItemRequest(product.Id, product.Name, item.Quantity, product.Price, 0, product.TaxRate));
            }

            if (lines.Count == 0) throw new AppException("No hay productos válidos en el pedido.");

            var sale = await sales.CreateAsync(new CreateSaleRequest(
                lines, PaymentMethod.Pending, 0, 0, null,
                station.Id, session.Id, "Consumo desde la estación"), userId: null);

            return Results.Ok(sale);
        });

        return app;
    }

    private static async Task<Station> RequireStationAsync(HttpContext http, AppDbContext db)
    {
        var idText = http.Request.Headers["X-Station-Id"].ToString();
        var token = http.Request.Headers["X-Station-Token"].ToString();

        if (!int.TryParse(idText, out var id) || string.IsNullOrWhiteSpace(token))
            throw new AppException("Estación no identificada.", 401);

        var station = await db.Stations.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == id);
        if (station is null || string.IsNullOrWhiteSpace(station.DeviceToken) || station.DeviceToken != token)
            throw new AppException("Token de estación inválido.", 401);

        return station;
    }
}
