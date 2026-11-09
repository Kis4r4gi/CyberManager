using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public class ReportService
{
    private readonly Data.AppDbContext _db;
    private readonly ExchangeRateService _rates;
    private readonly SettingsService _settings;

    public ReportService(Data.AppDbContext db, ExchangeRateService rates, SettingsService settings)
    {
        _db = db;
        _rates = rates;
        _settings = settings;
    }

    public async Task<ReportSummaryDto> SummaryAsync(DateTime fromUtc, DateTime toUtc)
    {
        var rate = await _rates.GetRateAsync();
        var sales = await _db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedUtc >= fromUtc && s.CreatedUtc < toUtc)
            .ToListAsync();
        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s => s.StartedUtc >= fromUtc && s.StartedUtc < toUtc)
            .ToListAsync();

        var productSales = sales.Sum(s => s.Total);
        var timeSales = sessions.Sum(s => s.AmountCharged);
        var total = productSales + timeSales;

        return new ReportSummaryDto(
            Math.Round(productSales, 2),
            Math.Round(timeSales, 2),
            Math.Round(total, 2),
            Money.ToVes(total, rate),
            Math.Round(sales.Sum(s => s.Discount), 2),
            sales.Count,
            sessions.Count,
            sessions.Sum(s => SessionCalculator.ElapsedMinutes(s, s.EndedUtc ?? DateTime.UtcNow)),
            sales.Count > 0 ? Math.Round(productSales / sales.Count, 2) : 0,
            rate);
    }

    public async Task<List<SalesReportRow>> SalesByDayAsync(DateTime fromUtc, DateTime toUtc)
    {
        var rate = await _rates.GetRateAsync();
        var sales = await _db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedUtc >= fromUtc && s.CreatedUtc < toUtc)
            .Select(s => new { s.CreatedUtc, s.Total })
            .ToListAsync();
        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s => s.StartedUtc >= fromUtc && s.StartedUtc < toUtc)
            .Select(s => new { s.StartedUtc, s.AmountCharged })
            .ToListAsync();

        var dates = sales.Select(s => s.CreatedUtc.ToLocalTime().Date)
            .Concat(sessions.Select(s => s.StartedUtc.ToLocalTime().Date))
            .Distinct().OrderBy(d => d);

        var rows = new List<SalesReportRow>();
        foreach (var day in dates)
        {
            var daySales = sales.Where(s => s.CreatedUtc.ToLocalTime().Date == day).ToList();
            var daySessions = sessions.Where(s => s.StartedUtc.ToLocalTime().Date == day).ToList();
            var products = daySales.Sum(s => s.Total);
            var time = daySessions.Sum(s => s.AmountCharged);
            rows.Add(new SalesReportRow(day, daySales.Count, Math.Round(products, 2), Math.Round(time, 2),
                Math.Round(products + time, 2), Money.ToVes(products, rate), Money.ToVes(time, rate),
                Money.ToVes(products + time, rate)));
        }
        return rows;
    }

    public async Task<List<ProductReportRow>> ProductsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var rate = await _rates.GetRateAsync();
        var items = await _db.SaleItems.AsNoTracking().Include(i => i.Sale)
            .Where(i => i.Sale!.Status == SaleStatus.Completed
                        && i.Sale.CreatedUtc >= fromUtc && i.Sale.CreatedUtc < toUtc)
            .ToListAsync();

        return items
            .GroupBy(i => new { i.ProductId, i.Description })
            .Select(g => new ProductReportRow(g.Key.ProductId ?? 0, g.Key.Description,
                Math.Round(g.Sum(i => i.Quantity), 2), Math.Round(g.Sum(i => i.Total), 2),
                Money.ToVes(g.Sum(i => i.Total), rate)))
            .OrderByDescending(r => r.Total)
            .Take(200)
            .ToList();
    }

    public async Task<List<StationReportRow>> StationsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var rate = await _rates.GetRateAsync();
        var stations = await _db.Stations.AsNoTracking().ToListAsync();
        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s => s.StartedUtc >= fromUtc && s.StartedUtc < toUtc)
            .ToListAsync();
        var sales = await _db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.StationId != null
                        && s.CreatedUtc >= fromUtc && s.CreatedUtc < toUtc)
            .ToListAsync();

        return stations.Select(st =>
        {
            var stSessions = sessions.Where(s => s.StationId == st.Id).ToList();
            var time = stSessions.Sum(s => s.AmountCharged);
            var consumption = sales.Where(s => s.StationId == st.Id).Sum(s => s.Total);
            return new StationReportRow(st.Id, st.Name, stSessions.Count,
                stSessions.Sum(s => SessionCalculator.ElapsedMinutes(s, s.EndedUtc ?? DateTime.UtcNow)),
                Math.Round(time, 2), Math.Round(consumption, 2),
                Money.ToVes(time, rate), Money.ToVes(consumption, rate));
        }).OrderByDescending(r => r.TimeAmount + r.Consumption).ToList();
    }

    public async Task<List<InventoryReportRow>> InventoryAsync()
        => await _db.Products.AsNoTracking()
            .Where(p => p.Active)
            .OrderBy(p => p.Name)
            .Select(p => new InventoryReportRow(p.Id, p.Sku, p.Name, p.Stock, p.MinStock, p.Cost, p.Price))
            .ToListAsync();

    /// <summary>Cierre diario: ventas, consumos de tiempo, caja y pagos del día local indicado.</summary>
    public async Task<DailyClosingDto> DailyClosingAsync(DateTime date)
    {
        var day = date.Date;
        var fromUtc = day.ToUniversalTime();
        var toUtc = day.AddDays(1).ToUniversalTime();

        var rate = await _rates.GetRateAsync(day.AddHours(12));
        var businessName = await _settings.GetAsync(SettingsKeys.BusinessName, "CyberManager");
        var businessTaxId = await _settings.GetAsync(SettingsKeys.BusinessTaxId);

        var sales = await _db.Sales.AsNoTracking().Include(s => s.Items)
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedUtc >= fromUtc && s.CreatedUtc < toUtc)
            .ToListAsync();

        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s => s.StartedUtc >= fromUtc && s.StartedUtc < toUtc)
            .ToListAsync();

        var cashSessions = await _db.CashSessions.AsNoTracking()
            .Where(c => c.OpenedUtc >= fromUtc && c.OpenedUtc < toUtc)
            .ToListAsync();
        var cashIds = cashSessions.Select(c => c.Id).ToList();
        var movements = await _db.CashMovements.AsNoTracking()
            .Where(m => cashIds.Contains(m.CashSessionId))
            .ToListAsync();

        var supplierPayments = await _db.SupplierPayments.AsNoTracking().Include(p => p.Supplier)
            .Where(p => p.DateUtc >= fromUtc && p.DateUtc < toUtc)
            .OrderByDescending(p => p.DateUtc)
            .ToListAsync();

        var productSales = Math.Round(sales.Sum(s => s.Total), 2);
        var timeSales = Math.Round(sessions.Sum(s => s.AmountCharged), 2);
        var totalSales = Math.Round(productSales + timeSales, 2);

        var payments = new List<PaymentBreakdownRow>();
        foreach (var group in sales.GroupBy(s => s.PaymentMethod))
        {
            payments.Add(new PaymentBreakdownRow(group.Key, group.Count(),
                Math.Round(group.Sum(s => s.Total), 2),
                Math.Round(group.Sum(s => s.TotalVes), 2)));
        }

        if (timeSales > 0)
        {
            var cashRow = payments.FirstOrDefault(p => p.Method == PaymentMethod.Cash);
            if (cashRow is null)
            {
                payments.Add(new PaymentBreakdownRow(PaymentMethod.Cash, sessions.Count(s => s.AmountCharged > 0),
                    timeSales, Money.ToVes(timeSales, rate)));
            }
            else
            {
                var index = payments.IndexOf(cashRow);
                payments[index] = cashRow with
                {
                    Count = cashRow.Count + sessions.Count(s => s.AmountCharged > 0),
                    Total = Math.Round(cashRow.Total + timeSales, 2),
                    TotalVes = Math.Round(cashRow.TotalVes + Money.ToVes(timeSales, rate), 2)
                };
            }
        }

        var cashOpening = Math.Round(cashSessions.Sum(c => c.OpeningAmount), 2);
        var cashExpenses = Math.Round(-movements.Where(m => m.Type == CashMovementType.Expense).Sum(m => m.Amount), 2);
        var cashExpected = Math.Round(movements
            .Where(m => m.PaymentMethod is PaymentMethod.Cash or PaymentMethod.CashUsd)
            .Sum(m => m.Amount), 2);
        var closedCash = cashSessions.Where(c => !c.IsOpen && c.ClosingAmount is not null).ToList();
        decimal? cashCounted = closedCash.Count > 0 ? Math.Round(closedCash.Sum(c => c.ClosingAmount!.Value), 2) : null;
        var cashDifference = Math.Round(closedCash.Sum(c => c.Difference), 2);

        var supplierPaymentsTotal = Math.Round(supplierPayments.Sum(p => p.Amount), 2);
        var nonCashSupplierPayments = Math.Round(supplierPayments
            .Where(p => p.Method is not (PaymentMethod.Cash or PaymentMethod.CashUsd))
            .Sum(p => p.Amount), 2);

        var topProducts = sales
            .SelectMany(s => s.Items)
            .GroupBy(i => new { i.ProductId, i.Description })
            .Select(g => new ProductReportRow(g.Key.ProductId ?? 0, g.Key.Description,
                Math.Round(g.Sum(i => i.Quantity), 2), Math.Round(g.Sum(i => i.Total), 2),
                Money.ToVes(g.Sum(i => i.Total), rate)))
            .OrderByDescending(r => r.Total)
            .ToList();

        return new DailyClosingDto(
            day, businessName, businessTaxId, rate,
            productSales, timeSales, totalSales, Money.ToVes(totalSales, rate),
            Math.Round(sales.Sum(s => s.IgtfAmount), 2),
            Math.Round(sales.Sum(s => s.Discount), 2),
            sales.Count > 0 ? Math.Round(productSales / sales.Count, 2) : 0,
            sales.Count, sessions.Count,
            sessions.Sum(s => SessionCalculator.ElapsedMinutes(s, s.EndedUtc ?? DateTime.UtcNow)),
            cashOpening, cashExpected, cashCounted, cashDifference,
            cashExpenses, supplierPaymentsTotal,
            Math.Round(cashExpenses + nonCashSupplierPayments, 2),
            payments.OrderByDescending(p => p.Total).ToList(),
            supplierPayments.Select(p => new SupplierPaymentDto(p.Id, p.SupplierId,
                p.Supplier?.Name ?? "Sin proveedor", p.DateUtc, p.Amount, p.Method,
                p.Reference, p.Notes)).ToList(),
            topProducts);
    }
}
