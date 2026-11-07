using System.Security.Claims;
using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

public static class OperationEndpoints
{
    public static IEndpointRouteBuilder MapOperationEndpoints(this IEndpointRouteBuilder app)
    {
        MapSessions(app);
        MapSales(app);
        MapCash(app);
        MapStock(app);
        MapPurchases(app);
        MapReservations(app);
        return app;
    }

    private static void MapSessions(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sessions").WithTags("Sesiones").RequireAuthorization();

        g.MapGet("/active", async (SessionService sessions) => Results.Ok(await sessions.ActiveAsync()));
        g.MapGet("/{id:int}", async (int id, SessionService sessions) => Results.Ok(await sessions.GetAsync(id)));

        g.MapPost("/start", async (StartSessionRequest req, ClaimsPrincipal user, SessionService sessions) =>
            Results.Ok(await sessions.StartAsync(req, user.UserId())));

        g.MapPost("/{id:int}/stop", async (int id, StopSessionRequest req, ClaimsPrincipal user,
            SessionService sessions) =>
        {
            req = req with { SessionId = id };
            return Results.Ok(await sessions.StopAsync(req, user.UserId()));
        });

        g.MapPost("/{id:int}/extend", async (int id, ExtendSessionRequest req, ClaimsPrincipal user,
            SessionService sessions) =>
        {
            req = req with { SessionId = id };
            return Results.Ok(await sessions.ExtendAsync(req, user.UserId()));
        });

        g.MapPost("/{id:int}/pause", async (int id, PauseSessionRequest req, ClaimsPrincipal user,
            SessionService sessions) =>
            Results.Ok(await sessions.PauseAsync(id, req.Pause, user.UserId())));

        g.MapPost("/{id:int}/transfer", async (int id, TransferSessionRequest req, ClaimsPrincipal user,
            SessionService sessions) =>
        {
            req = req with { SessionId = id };
            return Results.Ok(await sessions.TransferAsync(req, user.UserId()));
        });
    }

    private static void MapSales(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sales").WithTags("Ventas").RequireAuthorization();

        g.MapPost("/", async (CreateSaleRequest req, ClaimsPrincipal user, SaleService sales) =>
            Results.Ok(await sales.CreateAsync(req, user.UserId())));

        g.MapGet("/", async (DateTime? from, DateTime? to, int? stationId, SaleService sales) =>
            Results.Ok(await sales.ListAsync(
                from ?? DateTime.Today.ToUniversalTime(),
                to ?? DateTime.Today.AddDays(1).ToUniversalTime(),
                stationId)));

        g.MapGet("/{id:long}", async (long id, SaleService sales) => Results.Ok(await sales.ToDtoAsync(id)));

        g.MapPost("/{id:long}/cancel", async (long id, ClaimsPrincipal user, SaleService sales) =>
            Results.Ok(await sales.CancelAsync(id, user.UserId())));

        g.MapGet("/{id:long}/invoice", async (long id, InvoiceService invoices) =>
            Results.Ok(await invoices.BuildAsync(id)));
    }

    private static void MapCash(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cash").WithTags("Caja").RequireAuthorization();

        g.MapGet("/current", async (CashService cash) => Results.Ok(await cash.GetCurrentAsync()));

        g.MapPost("/open", async (CashOpenRequest req, ClaimsPrincipal user, CashService cash) =>
            Results.Ok(await cash.OpenAsync(req, user.UserId())));

        g.MapPost("/close", async (CashCloseRequest req, ClaimsPrincipal user, CashService cash) =>
            Results.Ok(await cash.CloseAsync(req, user.UserId())));

        g.MapPost("/movements", async (CashMovementRequest req, ClaimsPrincipal user, CashService cash) =>
            Results.Ok(await cash.AddManualMovementAsync(req, user.UserId())));

        g.MapGet("/history", async (DateTime? from, DateTime? to, CashService cash) =>
            Results.Ok(await cash.HistoryAsync(
                from ?? DateTime.Today.AddDays(-30).ToUniversalTime(),
                to ?? DateTime.Today.AddDays(1).ToUniversalTime())));
    }

    private static void MapStock(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stock").WithTags("Inventario").RequireAuthorization();

        g.MapGet("/movements", async (AppDbContext db, int? productId, DateTime? from, DateTime? to,
            int take = 300) =>
        {
            var q = db.StockMovements.AsNoTracking().Include(m => m.Product).AsQueryable();
            if (productId is int pid) q = q.Where(m => m.ProductId == pid);
            if (from is DateTime f) q = q.Where(m => m.CreatedUtc >= f);
            if (to is DateTime t) q = q.Where(m => m.CreatedUtc <= t);

            var list = await q.OrderByDescending(m => m.CreatedUtc).Take(Math.Clamp(take, 1, 2000)).ToListAsync();
            var names = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.FullName);

            return Results.Ok(list.Select(m => new StockMovementDto(m.Id, m.ProductId,
                m.Product?.Name ?? string.Empty, m.Type, m.Quantity, m.UnitCost, m.BalanceAfter,
                m.Reference, m.Notes, m.CreatedUtc,
                m.UserId is int uid ? names.GetValueOrDefault(uid) : null)).ToList());
        });

        g.MapPost("/movements", async (StockMovementRequest req, ClaimsPrincipal user, StockService stock) =>
        {
            var signed = req.Type switch
            {
                StockMovementType.Waste or StockMovementType.Sale => -Math.Abs(req.Quantity),
                StockMovementType.Purchase or StockMovementType.Return or StockMovementType.Initial => Math.Abs(req.Quantity),
                _ => req.Quantity
            };

            var product = await stock.ApplyAsync(req.ProductId, req.Type, signed, req.UnitCost,
                req.Reference, user.UserId(), req.Notes);
            return Results.Ok(new { product.Id, product.Name, product.Stock });
        });
    }

    private static void MapPurchases(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/purchases").WithTags("Compras").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db, DateTime? from, DateTime? to) =>
        {
            var q = db.Purchases.AsNoTracking().Include(p => p.Supplier).Include(p => p.Items).AsQueryable();
            if (from is DateTime f) q = q.Where(p => p.DateUtc >= f);
            if (to is DateTime t) q = q.Where(p => p.DateUtc <= t);

            var list = await q.OrderByDescending(p => p.DateUtc).Take(300).ToListAsync();
            return Results.Ok(list.Select(p => new PurchaseDto(p.Id, p.SupplierId, p.Supplier?.Name,
                p.InvoiceNumber, p.DateUtc, p.Total, p.Notes,
                p.Items.Select(i => new PurchaseItemRequest(i.ProductId, i.Quantity, i.UnitCost)).ToList())).ToList());
        });

        g.MapPost("/", async (CreatePurchaseRequest req, ClaimsPrincipal user, AppDbContext db,
            StockService stock) =>
        {
            if (req.Items is null || req.Items.Count == 0) throw new AppException("La compra no tiene artículos.");

            await using var tx = await db.Database.BeginTransactionAsync();

            var purchase = new Purchase
            {
                SupplierId = req.SupplierId,
                InvoiceNumber = req.InvoiceNumber,
                Notes = req.Notes,
                UserId = user.UserId(),
                DateUtc = DateTime.UtcNow,
                Total = req.Items.Sum(i => i.Quantity * i.UnitCost)
            };
            db.Purchases.Add(purchase);
            await db.SaveChangesAsync();

            foreach (var item in req.Items)
            {
                if (item.Quantity <= 0) throw new AppException("La cantidad debe ser mayor que cero.");
                db.PurchaseItems.Add(new PurchaseItem
                {
                    PurchaseId = purchase.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost
                });
                await stock.ApplyAsync(item.ProductId, StockMovementType.Purchase, item.Quantity,
                    item.UnitCost, $"COMPRA-{purchase.Id}", user.UserId(), req.Notes);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { purchase.Id, purchase.Total });
        }).RequireAuthorization("Admin");
    }

    private static void MapReservations(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reservations").WithTags("Reservas").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db, DateTime? from, DateTime? to) =>
        {
            var q = db.Reservations.AsNoTracking().Include(r => r.Station).AsQueryable();
            if (from is DateTime f) q = q.Where(r => r.EndUtc >= f);
            if (to is DateTime t) q = q.Where(r => r.StartUtc <= t);

            var list = await q.OrderBy(r => r.StartUtc).Take(300).ToListAsync();
            return Results.Ok(list.Select(r => new ReservationDto(r.Id, r.StationId,
                r.Station?.Name ?? string.Empty, r.CustomerId, r.CustomerName, r.Phone,
                r.StartUtc, r.EndUtc, r.Status, r.Notes)).ToList());
        });

        g.MapPost("/", async (SaveReservationRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.CustomerName)) throw new AppException("Indique el nombre del cliente.");
            if (req.EndUtc <= req.StartUtc) throw new AppException("La hora final debe ser mayor a la inicial.");

            var r = new Reservation
            {
                StationId = req.StationId, CustomerId = req.CustomerId, CustomerName = req.CustomerName,
                Phone = req.Phone, StartUtc = req.StartUtc, EndUtc = req.EndUtc,
                Status = req.Status, Notes = req.Notes
            };
            db.Reservations.Add(r);
            await db.SaveChangesAsync();
            return Results.Ok(r.Id);
        });

        g.MapPut("/{id:int}", async (int id, SaveReservationRequest req, AppDbContext db) =>
        {
            var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Reserva");
            r.StationId = req.StationId; r.CustomerId = req.CustomerId; r.CustomerName = req.CustomerName;
            r.Phone = req.Phone; r.StartUtc = req.StartUtc; r.EndUtc = req.EndUtc;
            r.Status = req.Status; r.Notes = req.Notes;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Reserva");
            r.Status = ReservationStatus.Cancelled;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
