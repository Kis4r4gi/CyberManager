using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public class DashboardService
{
    private readonly Data.AppDbContext _db;
    private readonly CashService _cash;
    private readonly ExchangeRateService _rates;

    public DashboardService(Data.AppDbContext db, CashService cash, ExchangeRateService rates)
    {
        _db = db;
        _cash = cash;
        _rates = rates;
    }

    public async Task<DashboardDto> BuildAsync()
    {
        var now = DateTime.UtcNow;
        var startUtc = DateTime.Today.ToUniversalTime();
        var endUtc = startUtc.AddDays(1);

        var rate = await _rates.GetRateAsync();
        var stations = await _db.Stations.AsNoTracking().Include(s => s.Zone)
            .Where(s => s.Active).OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync();

        var sessions = await _db.Sessions.AsNoTracking().Include(s => s.Station)
            .Where(s => s.Status != SessionStatus.Closed && s.Status != SessionStatus.Cancelled)
            .ToListAsync();

        var salesToday = await _db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedUtc >= startUtc && s.CreatedUtc < endUtc)
            .ToListAsync();

        var sessionsToday = await _db.Sessions.AsNoTracking()
            .Where(s => s.StartedUtc >= startUtc && s.StartedUtc < endUtc).ToListAsync();

        var productSales = salesToday.Sum(s => s.Total);
        var timeSales = sessionsToday.Sum(s => s.AmountCharged);
        var total = Math.Round(productSales + timeSales, 2);

        var consumptionToday = salesToday.Where(s => s.SessionId.HasValue).Sum(s => s.Total);

        decimal cashInDrawer = 0;
        var openCash = await _cash.GetOpenAsync();
        if (openCash is not null)
            cashInDrawer = (await _cash.ToDtoAsync(openCash)).ExpectedAmount;

        var lowStock = await _db.Products.AsNoTracking()
            .CountAsync(p => p.Active && !p.IsService && p.Stock <= p.MinStock);

        var pendingReservations = await _db.Reservations.AsNoTracking()
            .CountAsync(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                             && r.EndUtc >= now);

        var summaries = new List<StationSummaryDto>();
        foreach (var st in stations)
        {
            var session = sessions.FirstOrDefault(s => s.StationId == st.Id);
            if (session is null)
            {
                summaries.Add(new StationSummaryDto(st.Id, st.Name, st.Zone?.Name, st.Status,
                    null, 0, 0, 0, null, false));
                continue;
            }

            var elapsed = SessionCalculator.ElapsedMinutes(session, now);
            var consumptionTotals = await _db.Sales.AsNoTracking()
                .Where(s => s.SessionId == session.Id && s.Status == SaleStatus.Completed)
                .Select(s => s.Total)
                .ToListAsync();
            var consumption = consumptionTotals.Sum();
            var amount = Math.Round(SessionCalculator.TimeAmount(session, elapsed) + consumption, 2);

            summaries.Add(new StationSummaryDto(st.Id, st.Name, st.Zone?.Name, st.Status,
                session.Id, elapsed, SessionCalculator.RemainingMinutes(session, elapsed),
                amount, session.Type, SessionCalculator.IsOvertime(session, elapsed)));
        }

        return new DashboardDto(
            stations.Count,
            stations.Count(s => s.Status != StationStatus.Offline),
            stations.Count(s => s.Status == StationStatus.Available),
            stations.Count(s => s.Status == StationStatus.InUse),
            stations.Count(s => s.Status == StationStatus.Paused),
            stations.Count(s => s.Status == StationStatus.Maintenance),
            sessions.Count,
            Math.Round(productSales, 2),
            Math.Round(timeSales, 2),
            total,
            Money.ToVes(total, rate),
            rate,
            Math.Round(cashInDrawer, 2),
            Money.ToVes(cashInDrawer, rate),
            lowStock,
            sessionsToday.Count,
            pendingReservations,
            Math.Round(consumptionToday, 2),
            summaries);
    }
}
