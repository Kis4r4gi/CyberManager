using System.Security.Claims;
using System.Text.Json;
using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        MapZones(app);
        MapRates(app);
        MapPackages(app);
        MapCategories(app);
        MapProducts(app);
        MapSuppliers(app);
        MapCustomers(app);
        MapExchangeRates(app);
        MapSettings(app);
        MapBootstrap(app);
        return app;
    }

    private static void MapZones(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/zones").WithTags("Zonas").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Zones.AsNoTracking()
            .OrderBy(z => z.SortOrder)
            .Select(z => new ZoneDto(z.Id, z.Name, z.Color, z.SortOrder, z.Active)).ToListAsync()));

        g.MapPost("/", async (SaveZoneRequest req, AppDbContext db) =>
        {
            var z = new Zone { Name = req.Name, Color = req.Color, SortOrder = req.SortOrder, Active = req.Active };
            db.Zones.Add(z);
            await db.SaveChangesAsync();
            return Results.Ok(new ZoneDto(z.Id, z.Name, z.Color, z.SortOrder, z.Active));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveZoneRequest req, AppDbContext db) =>
        {
            var z = await db.Zones.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Zona");
            z.Name = req.Name; z.Color = req.Color; z.SortOrder = req.SortOrder; z.Active = req.Active;
            await db.SaveChangesAsync();
            return Results.Ok(new ZoneDto(z.Id, z.Name, z.Color, z.SortOrder, z.Active));
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var z = await db.Zones.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Zona");
            if (await db.Stations.AnyAsync(s => s.ZoneId == id))
                throw new AppException("La zona tiene equipos asignados.");
            z.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");
    }

    private static void MapRates(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/rates").WithTags("Tarifas").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Rates.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RateDto(r.Id, r.Name, r.PricePerHour, r.MinimumMinutes, r.BillingBlockMinutes, r.Active))
            .ToListAsync()));

        g.MapPost("/", async (SaveRateRequest req, AppDbContext db) =>
        {
            if (req.PricePerHour <= 0) throw new AppException("El precio por hora debe ser mayor que cero.");
            var r = new Rate
            {
                Name = req.Name, PricePerHour = req.PricePerHour, MinimumMinutes = req.MinimumMinutes,
                BillingBlockMinutes = req.BillingBlockMinutes, Active = req.Active
            };
            db.Rates.Add(r);
            await db.SaveChangesAsync();
            return Results.Ok(new RateDto(r.Id, r.Name, r.PricePerHour, r.MinimumMinutes, r.BillingBlockMinutes, r.Active));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveRateRequest req, AppDbContext db) =>
        {
            var r = await db.Rates.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Tarifa");
            r.Name = req.Name; r.PricePerHour = req.PricePerHour; r.MinimumMinutes = req.MinimumMinutes;
            r.BillingBlockMinutes = req.BillingBlockMinutes; r.Active = req.Active;
            await db.SaveChangesAsync();
            return Results.Ok(new RateDto(r.Id, r.Name, r.PricePerHour, r.MinimumMinutes, r.BillingBlockMinutes, r.Active));
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var r = await db.Rates.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Tarifa");
            if (await db.Stations.AnyAsync(s => s.RateId == id))
                throw new AppException("La tarifa está asignada a equipos.");
            r.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");
    }

    private static void MapPackages(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/time-packages").WithTags("Paquetes de tiempo").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) =>
        {
            // SQLite no puede ordenar por decimal en SQL: se ordena en memoria.
            var packages = await db.TimePackages.AsNoTracking()
                .Select(p => new TimePackageDto(p.Id, p.Name, p.Minutes, p.BonusMinutes, p.Price, p.Active))
                .ToListAsync();
            return Results.Ok(packages.OrderBy(p => p.Price).ThenBy(p => p.Minutes).ToList());
        });

        g.MapPost("/", async (SaveTimePackageRequest req, AppDbContext db) =>
        {
            var p = new TimePackage
            {
                Name = req.Name, Minutes = req.Minutes, BonusMinutes = req.BonusMinutes,
                Price = req.Price, Active = req.Active
            };
            db.TimePackages.Add(p);
            await db.SaveChangesAsync();
            return Results.Ok(new TimePackageDto(p.Id, p.Name, p.Minutes, p.BonusMinutes, p.Price, p.Active));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveTimePackageRequest req, AppDbContext db) =>
        {
            var p = await db.TimePackages.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Paquete");
            p.Name = req.Name; p.Minutes = req.Minutes; p.BonusMinutes = req.BonusMinutes;
            p.Price = req.Price; p.Active = req.Active;
            await db.SaveChangesAsync();
            return Results.Ok(new TimePackageDto(p.Id, p.Name, p.Minutes, p.BonusMinutes, p.Price, p.Active));
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var p = await db.TimePackages.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Paquete");
            p.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");
    }

    private static void MapCategories(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/categories").WithTags("Categorías").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.Active)).ToListAsync()));

        g.MapPost("/", async (SaveCategoryRequest req, AppDbContext db) =>
        {
            var c = new Category { Name = req.Name, Color = req.Color, SortOrder = req.SortOrder, Active = req.Active };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new CategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.Active));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveCategoryRequest req, AppDbContext db) =>
        {
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Categoría");
            c.Name = req.Name; c.Color = req.Color; c.SortOrder = req.SortOrder; c.Active = req.Active;
            await db.SaveChangesAsync();
            return Results.Ok(new CategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.Active));
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Categoría");
            c.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");
    }

    private static void MapProducts(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/products").WithTags("Productos").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db, string? q, int? categoryId, bool includeInactive = false) =>
        {
            var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
            if (!includeInactive) query = query.Where(p => p.Active);
            if (categoryId is int cid) query = query.Where(p => p.CategoryId == cid);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
            }

            var list = await query.OrderBy(p => p.Name).ToListAsync();
            return Results.Ok(list.Select(Mappers.ToDto).ToList());
        });

        g.MapPost("/", async (SaveProductRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("El nombre es obligatorio.");
            if (await db.Products.AnyAsync(p => p.Sku == req.Sku))
                throw new AppException("Ya existe un producto con ese SKU.", 409);

            var p = new Product
            {
                Sku = req.Sku, Name = req.Name, CategoryId = req.CategoryId, Price = req.Price,
                Cost = req.Cost, Stock = req.Stock, MinStock = req.MinStock, Unit = req.Unit,
                IsService = req.IsService, TaxRate = req.TaxRate, Active = req.Active, Icon = req.Icon
            };
            db.Products.Add(p);
            await db.SaveChangesAsync();

            if (req.Stock > 0)
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = p.Id, Type = StockMovementType.Initial, Quantity = req.Stock,
                    UnitCost = req.Cost, BalanceAfter = req.Stock, Notes = "Stock inicial",
                    Reference = "ALTA", CreatedUtc = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
            return Results.Ok(Mappers.ToDto(p));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveProductRequest req, AppDbContext db) =>
        {
            var p = await db.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id)
                    ?? throw AppException.NotFound("Producto");
            p.Sku = req.Sku; p.Name = req.Name; p.CategoryId = req.CategoryId; p.Price = req.Price;
            p.Cost = req.Cost; p.MinStock = req.MinStock; p.Unit = req.Unit;
            p.IsService = req.IsService; p.TaxRate = req.TaxRate; p.Active = req.Active; p.Icon = req.Icon;
            await db.SaveChangesAsync();
            return Results.Ok(Mappers.ToDto(p));
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Producto");
            p.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");
    }

    private static void MapSuppliers(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/suppliers").WithTags("Proveedores").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Suppliers.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Address, s.Notes, s.Active))
            .ToListAsync()));

        g.MapPost("/", async (SaveSupplierRequest req, AppDbContext db) =>
        {
            var s = new Supplier
            {
                Name = req.Name, Phone = req.Phone, Email = req.Email,
                Address = req.Address, Notes = req.Notes, Active = req.Active
            };
            db.Suppliers.Add(s);
            await db.SaveChangesAsync();
            return Results.Ok(new SupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Address, s.Notes, s.Active));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveSupplierRequest req, AppDbContext db) =>
        {
            var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Proveedor");
            s.Name = req.Name; s.Phone = req.Phone; s.Email = req.Email;
            s.Address = req.Address; s.Notes = req.Notes; s.Active = req.Active;
            await db.SaveChangesAsync();
            return Results.Ok(new SupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Address, s.Notes, s.Active));
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Proveedor");
            s.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");

        // ---------- Saldos y pagos a proveedores ----------
        g.MapGet("/balances", async (AppDbContext db) =>
        {
            var suppliers = await db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
            var purchases = await db.Purchases.AsNoTracking()
                .Where(p => p.SupplierId != null)
                .Select(p => new { SupplierId = p.SupplierId!.Value, p.Total })
                .ToListAsync();
            var payments = await db.SupplierPayments.AsNoTracking()
                .Select(p => new { p.SupplierId, p.Amount })
                .ToListAsync();

            return Results.Ok(suppliers.Select(s =>
            {
                var bought = purchases.Where(p => p.SupplierId == s.Id).Sum(p => p.Total);
                var paid = payments.Where(p => p.SupplierId == s.Id).Sum(p => p.Amount);
                return new SupplierBalanceDto(s.Id, s.Name, Math.Round(bought, 2),
                    Math.Round(paid, 2), Math.Round(bought - paid, 2));
            }).ToList());
        });

        g.MapGet("/{id:int}/payments", async (int id, AppDbContext db) =>
        {
            var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id)
                           ?? throw AppException.NotFound("Proveedor");

            var payments = await db.SupplierPayments.AsNoTracking()
                .Where(p => p.SupplierId == id)
                .OrderByDescending(p => p.DateUtc)
                .Take(300)
                .ToListAsync();

            return Results.Ok(payments.Select(p => new SupplierPaymentDto(p.Id, p.SupplierId,
                supplier.Name, p.DateUtc, p.Amount, p.Method, p.Reference, p.Notes)).ToList());
        });

        g.MapPost("/{id:int}/payments", async (int id, CreateSupplierPaymentRequest req,
            ClaimsPrincipal user, AppDbContext db, CashService cash) =>
        {
            if (req.Amount <= 0) throw new AppException("El monto del pago debe ser mayor que cero.");
            if (req.Method is PaymentMethod.Pending or PaymentMethod.Mixed or PaymentMethod.CustomerBalance)
                throw new AppException("Seleccione una forma de pago válida para el proveedor.");

            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id)
                           ?? throw AppException.NotFound("Proveedor");

            await using var tx = await db.Database.BeginTransactionAsync();

            if (req.Method is PaymentMethod.Cash or PaymentMethod.CashUsd)
            {
                var open = await cash.GetOpenAsync()
                           ?? throw new AppException("Abra la caja para registrar pagos en efectivo.", 409);
                await cash.AddMovementAsync(open.Id, CashMovementType.Expense, req.Method, -req.Amount,
                    $"Pago a proveedor: {supplier.Name}", user.UserId());
            }

            var payment = new SupplierPayment
            {
                SupplierId = id,
                DateUtc = DateTime.UtcNow,
                Amount = req.Amount,
                Method = req.Method,
                Reference = req.Reference,
                Notes = req.Notes,
                UserId = user.UserId()
            };
            db.SupplierPayments.Add(payment);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new SupplierPaymentDto(payment.Id, payment.SupplierId, supplier.Name,
                payment.DateUtc, payment.Amount, payment.Method, payment.Reference, payment.Notes));
        }).RequireAuthorization("Admin");

        var all = app.MapGroup("/api/supplier-payments").WithTags("Pagos a proveedores").RequireAuthorization();

        all.MapGet("/", async (AppDbContext db, DateTime? from, DateTime? to) =>
        {
            var q = db.SupplierPayments.AsNoTracking().Include(p => p.Supplier).AsQueryable();
            if (from is DateTime f) q = q.Where(p => p.DateUtc >= f);
            if (to is DateTime t) q = q.Where(p => p.DateUtc <= t);

            var list = await q.OrderByDescending(p => p.DateUtc).Take(500).ToListAsync();
            return Results.Ok(list.Select(p => new SupplierPaymentDto(p.Id, p.SupplierId,
                p.Supplier?.Name ?? "Sin proveedor", p.DateUtc, p.Amount, p.Method,
                p.Reference, p.Notes)).ToList());
        });
    }

    private static void MapCustomers(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/customers").WithTags("Clientes").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db, string? q, bool includeInactive = false) =>
        {
            var query = db.Customers.AsNoTracking().AsQueryable();
            if (!includeInactive) query = query.Where(c => c.Active);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(term)
                    || (c.Phone != null && c.Phone.Contains(term))
                    || (c.DocumentId != null && c.DocumentId.Contains(term)));
            }
            var list = await query.OrderBy(c => c.Name).Take(500).ToListAsync();
            return Results.Ok(list.Select(Mappers.ToDto).ToList());
        });

        g.MapPost("/", async (SaveCustomerRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("El nombre es obligatorio.");
            var c = new Customer
            {
                Name = req.Name, Phone = req.Phone, Email = req.Email, DocumentId = req.DocumentId,
                Address = req.Address, FiscalData = req.FiscalData, CreditLimit = req.CreditLimit,
                IsMember = req.IsMember, Active = req.Active, Notes = req.Notes
            };
            db.Customers.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(Mappers.ToDto(c));
        });

        g.MapPut("/{id:int}", async (int id, SaveCustomerRequest req, AppDbContext db) =>
        {
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Cliente");
            c.Name = req.Name; c.Phone = req.Phone; c.Email = req.Email; c.DocumentId = req.DocumentId;
            c.Address = req.Address; c.FiscalData = req.FiscalData; c.CreditLimit = req.CreditLimit;
            c.IsMember = req.IsMember; c.Active = req.Active; c.Notes = req.Notes;
            await db.SaveChangesAsync();
            return Results.Ok(Mappers.ToDto(c));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Cliente");
            c.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Admin");

        g.MapGet("/{id:int}/transactions", async (int id, AppDbContext db) =>
            Results.Ok(await db.CustomerTransactions.AsNoTracking()
                .Where(t => t.CustomerId == id).OrderByDescending(t => t.CreatedUtc).Take(200)
                .Select(t => new CustomerTransactionDto(t.Id, t.CustomerId, t.Type, t.Amount,
                    t.BalanceAfter, t.Description, t.CreatedUtc)).ToListAsync()));

        g.MapPost("/{id:int}/recharge", async (int id, RechargeRequest req, ClaimsPrincipal principal,
            AppDbContext db, CashService cash) =>
        {
            if (req.Amount <= 0) throw new AppException("El monto debe ser mayor que cero.");
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Cliente");

            if (req.Method is PaymentMethod.Cash or PaymentMethod.CashUsd or PaymentMethod.Card
                or PaymentMethod.Transfer or PaymentMethod.PagoMovil)
            {
                var cs = await cash.GetOpenAsync()
                         ?? throw new AppException("Debe abrir la caja para recargar saldo.", 409);
                await cash.AddMovementAsync(cs.Id, CashMovementType.Deposit, req.Method, req.Amount,
                    $"Recarga saldo {c.Name}", principal.UserId());
            }

            c.Balance = Math.Round(c.Balance + req.Amount, 2);
            db.CustomerTransactions.Add(new CustomerTransaction
            {
                CustomerId = c.Id, Type = CustomerTransactionType.Recharge, Amount = req.Amount,
                BalanceAfter = c.Balance, PaymentMethod = req.Method,
                Description = req.Description ?? "Recarga de saldo", UserId = principal.UserId()
            });
            await db.SaveChangesAsync();
            return Results.Ok(Mappers.ToDto(c));
        });
    }

    private static void MapExchangeRates(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/exchange-rates").WithTags("Tasas de cambio").RequireAuthorization();

        g.MapGet("/today", async (ExchangeRateService rates) =>
        {
            var r = await rates.GetActiveAsync();
            return r is null
                ? Results.Ok(new ExchangeRateDto(0, ExchangeRateService.LocalDate(), 0, RateSource.Manual, false, DateTime.UtcNow, null))
                : Results.Ok(new ExchangeRateDto(r.Id, r.Date, r.Rate, r.Source, r.IsActive, r.CreatedUtc, r.Notes));
        });

        g.MapGet("/", async (DateTime? from, DateTime? to, ExchangeRateService rates) =>
            Results.Ok(await rates.HistoryAsync(from ?? DateTime.Today.AddDays(-30), to ?? DateTime.Today)));

        g.MapPost("/", async (SetExchangeRateRequest req, ClaimsPrincipal principal,
            ExchangeRateService rates) =>
            Results.Ok(await rates.SetAsync(req, principal.UserId())));

        g.MapPost("/bcv", async (ClaimsPrincipal principal, BcvRateService bcv) =>
                Results.Ok(await bcv.UpdateAsync(principal.UserId())))
            .RequireAuthorization("Admin");

        g.MapGet("/suggested", async (IHttpClientFactory factory) =>
        {
            try
            {
                var client = factory.CreateClient("rates");
                using var doc = JsonDocument.Parse(await client.GetStringAsync("https://ve.dolarapi.com/v1/dolares"));
                decimal? official = null, parallel = null;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var fuente = item.TryGetProperty("fuente", out var f) ? f.GetString() : null;
                    if (!item.TryGetProperty("promedio", out var p) || p.ValueKind != JsonValueKind.Number) continue;
                    var value = p.GetDecimal();
                    if (string.Equals(fuente, "oficial", StringComparison.OrdinalIgnoreCase)) official = value;
                    else if (string.Equals(fuente, "paralelo", StringComparison.OrdinalIgnoreCase)) parallel = value;
                }
                return Results.Ok(new { official, parallel });
            }
            catch
            {
                return Results.Ok(new { official = (decimal?)null, parallel = (decimal?)null });
            }
        });
    }

    private static void MapSettings(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings").WithTags("Configuración").RequireAuthorization();

        g.MapGet("/", async (SettingsService settings) => Results.Ok(await settings.GetAllAsync()));

        g.MapPut("/", async (Dictionary<string, string> values, SettingsService settings) =>
        {
            await settings.SetManyAsync(values);
            return Results.Ok(await settings.GetAllAsync());
        }).RequireAuthorization("Admin");
    }

    private static void MapBootstrap(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog", async (AppDbContext db, SettingsService settings, ExchangeRateService rates) =>
        {
            var rate = await rates.GetActiveAsync();
            return Results.Ok(new
            {
                zones = await db.Zones.AsNoTracking().OrderBy(z => z.SortOrder)
                    .Select(z => new ZoneDto(z.Id, z.Name, z.Color, z.SortOrder, z.Active)).ToListAsync(),
                rates = await db.Rates.AsNoTracking()
                    .Select(r => new RateDto(r.Id, r.Name, r.PricePerHour, r.MinimumMinutes, r.BillingBlockMinutes, r.Active)).ToListAsync(),
                packages = await db.TimePackages.AsNoTracking()
                    .Select(p => new TimePackageDto(p.Id, p.Name, p.Minutes, p.BonusMinutes, p.Price, p.Active)).ToListAsync(),
                categories = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder)
                    .Select(c => new CategoryDto(c.Id, c.Name, c.Color, c.SortOrder, c.Active)).ToListAsync(),
                products = (await db.Products.AsNoTracking().Include(p => p.Category)
                    .Where(p => p.Active).OrderBy(p => p.Name).ToListAsync()).Select(Mappers.ToDto).ToList(),
                settings = await settings.GetAllAsync(),
                exchangeRate = rate is null ? null : new ExchangeRateDto(rate.Id, rate.Date, rate.Rate, rate.Source, rate.IsActive, rate.CreatedUtc, rate.Notes)
            });
        }).RequireAuthorization();
    }
}
