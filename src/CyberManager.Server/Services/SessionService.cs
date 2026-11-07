using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public class SessionService
{
    private readonly Data.AppDbContext _db;
    private readonly ExchangeRateService _rates;
    private readonly SettingsService _settings;
    private readonly CashService _cash;
    private readonly RealtimeService _realtime;

    public SessionService(Data.AppDbContext db, ExchangeRateService rates, SettingsService settings,
        CashService cash, RealtimeService realtime)
    {
        _db = db;
        _rates = rates;
        _settings = settings;
        _cash = cash;
        _realtime = realtime;
    }

    private async Task<decimal> ConsumptionAsync(int sessionId)
    {
        var totals = await _db.Sales.AsNoTracking()
            .Where(s => s.SessionId == sessionId && s.Status == SaleStatus.Completed)
            .Select(s => s.Total)
            .ToListAsync();
        return totals.Sum();
    }

    private async Task<decimal> PendingConsumptionAsync(int sessionId)
    {
        var totals = await _db.Sales.AsNoTracking()
            .Where(s => s.SessionId == sessionId && s.Status == SaleStatus.Completed
                        && s.PaymentMethod == PaymentMethod.Pending)
            .Select(s => s.Total)
            .ToListAsync();
        return totals.Sum();
    }

    public async Task<SessionDto> StartAsync(StartSessionRequest req, int userId)
    {
        var station = await _db.Stations.Include(s => s.Rate)
            .FirstOrDefaultAsync(s => s.Id == req.StationId)
            ?? throw AppException.NotFound("Equipo");

        if (!station.Active) throw new AppException("El equipo está deshabilitado.");
        if (station.Status is StationStatus.InUse or StationStatus.Paused)
            throw new AppException("El equipo ya tiene una sesión activa.");
        if (station.Status == StationStatus.Maintenance)
            throw new AppException("El equipo está en mantenimiento.");

        Rate? rate = null;
        if (req.RateId is int rid)
            rate = await _db.Rates.FirstOrDefaultAsync(r => r.Id == rid && r.Active)
                   ?? throw AppException.NotFound("Tarifa");
        else if (station.RateId is not null)
            rate = station.Rate;
        rate ??= await _db.Rates.FirstOrDefaultAsync(r => r.Active);

        if (rate is null) throw new AppException("No hay tarifas configuradas.");

        var ratePerHour = req.CustomRatePerHour ?? rate.PricePerHour;
        if (ratePerHour <= 0) throw new AppException("La tarifa debe ser mayor que cero.");

        if (req.Type == SessionType.Prepaid && req.PlannedMinutes <= 0)
            throw new AppException("Indique los minutos a prepagar.");

        var upfront = req.Type == SessionType.Prepaid
            ? req.UpfrontAmount ?? Math.Round(ratePerHour * req.PlannedMinutes / 60m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        if (req.Type == SessionType.Free) upfront = 0m;

        CashSession? cash = null;
        if (req.ChargeUpfront && upfront > 0)
        {
            cash = await _cash.GetOpenAsync()
                   ?? throw new AppException("Debe abrir la caja para cobrar el tiempo.", 409);
        }

        Customer? customer = null;
        if (req.CustomerId is int cid)
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == cid)
                       ?? throw AppException.NotFound("Cliente");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var session = new Session
        {
            StationId = station.Id,
            CustomerId = req.CustomerId,
            Type = req.Type,
            Status = SessionStatus.Active,
            StartedUtc = DateTime.UtcNow,
            PlannedMinutes = req.PlannedMinutes,
            RatePerHour = ratePerHour,
            MinimumMinutes = rate.MinimumMinutes,
            BillingBlockMinutes = rate.BillingBlockMinutes,
            AmountCharged = req.ChargeUpfront ? upfront : 0m,
            OpenedByUserId = userId,
            Notes = req.Notes,
            CashSessionId = cash?.Id
        };
        _db.Sessions.Add(session);

        station.Status = StationStatus.InUse;
        await _db.SaveChangesAsync();
        station.CurrentSessionId = session.Id;

        if (req.ChargeUpfront && upfront > 0 && cash is not null)
        {
            await _cash.AddMovementAsync(cash.Id, CashMovementType.TimeSale, req.UpfrontMethod, upfront,
                $"Tiempo {req.PlannedMinutes} min - {station.Name}", userId, sessionId: session.Id);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await NotifyStationAsync(station, session);
        await _realtime.SendAgentCommandAsync(station.Id, AgentCommandType.Unlock);
        await _realtime.SendStationCommandAsync(station.Id, AgentCommandType.Unlock);
        await _realtime.BroadcastEventAsync("SessionStarted", $"{station.Name}: sesión iniciada", session.Id);

        return Mappers.ToDto(session, 0m, DateTime.UtcNow);
    }

    public async Task<SessionDto> StopAsync(StopSessionRequest req, int userId)
    {
        var session = await _db.Sessions.Include(s => s.Station).Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == req.SessionId)
            ?? throw AppException.NotFound("Sesión");

        if (session.Status is SessionStatus.Closed or SessionStatus.Cancelled)
            throw new AppException("La sesión ya está cerrada.");

        var station = session.Station ?? await _db.Stations.FirstAsync(s => s.Id == session.StationId);
        var now = DateTime.UtcNow;
        var elapsed = SessionCalculator.ElapsedMinutes(session, now);
        var timeAmount = SessionCalculator.TimeAmount(session, elapsed);

        var pending = await PendingConsumptionAsync(session.Id);
        var consumption = await ConsumptionAsync(session.Id);

        decimal toCharge = 0m;
        if (session.Type == SessionType.Postpaid)
        {
            if (req.ChargeTime && session.Status != SessionStatus.Cancelled)
            {
                toCharge += timeAmount;
                session.AmountCharged = timeAmount;
            }
        }
        else if (session.Type == SessionType.Prepaid && elapsed > session.PlannedMinutes)
        {
            var extraMinutes = elapsed - session.PlannedMinutes;
            var extra = Math.Round(session.RatePerHour * extraMinutes / 60m, 2, MidpointRounding.AwayFromZero);
            if (req.ChargeTime)
            {
                toCharge += extra;
                session.AmountCharged = Math.Round(session.AmountCharged + extra, 2);
            }
        }

        toCharge += pending;
        toCharge = Math.Round(toCharge, 2, MidpointRounding.AwayFromZero);

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (toCharge > 0)
        {
            var cash = await _cash.GetOpenAsync();
            if (cash is null)
                throw new AppException("Debe haber una caja abierta para cobrar la sesión.", 409);

            if (req.Method == PaymentMethod.CustomerBalance)
            {
                var customer = session.Customer
                    ?? throw new AppException("Seleccione un cliente para cobrar a su cuenta.");
                if (customer.Balance < toCharge)
                    throw new AppException($"Saldo insuficiente. Disponible: {customer.Balance:F2} USD.");
                customer.Balance = Math.Round(customer.Balance - toCharge, 2);
                _db.CustomerTransactions.Add(new CustomerTransaction
                {
                    CustomerId = customer.Id,
                    Type = CustomerTransactionType.Consumption,
                    Amount = -toCharge,
                    BalanceAfter = customer.Balance,
                    Description = $"Sesión {station.Name}",
                    UserId = userId
                });
            }
            else
            {
                await _cash.AddMovementAsync(cash.Id, CashMovementType.TimeSale, req.Method, toCharge,
                    $"Cierre {station.Name} ({elapsed} min)", userId, sessionId: session.Id);
            }

            var pendingSales = await _db.Sales
                .Where(s => s.SessionId == session.Id && s.Status == SaleStatus.Completed
                            && s.PaymentMethod == PaymentMethod.Pending)
                .ToListAsync();
            foreach (var sale in pendingSales)
            {
                sale.PaymentMethod = req.Method;
                sale.PaidAmount = sale.Total;
            }
        }

        session.Status = SessionStatus.Closed;
        session.EndedUtc = now;
        session.ClosedByUserId = userId;
        session.CashSessionId ??= (await _cash.GetOpenAsync())?.Id;
        if (session.PausedAtUtc is not null)
        {
            session.PausedSeconds += (int)(now - session.PausedAtUtc.Value).TotalSeconds;
            session.PausedAtUtc = null;
        }
        if (!string.IsNullOrWhiteSpace(req.Notes)) session.Notes = req.Notes;

        station.Status = StationStatus.Available;
        station.CurrentSessionId = null;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _realtime.SendAgentCommandAsync(station.Id, AgentCommandType.Lock);
        await _realtime.SendStationCommandAsync(station.Id, AgentCommandType.Lock);
        await NotifyStationAsync(station, session);
        await _realtime.BroadcastEventAsync("SessionEnded", $"{station.Name}: sesión cerrada", session.Id);

        return Mappers.ToDto(session, consumption, now);
    }

    public async Task<SessionDto> ExtendAsync(ExtendSessionRequest req, int userId)
    {
        var session = await _db.Sessions.Include(s => s.Station)
            .FirstOrDefaultAsync(s => s.Id == req.SessionId)
            ?? throw AppException.NotFound("Sesión");
        if (session.Status is SessionStatus.Closed or SessionStatus.Cancelled)
            throw new AppException("La sesión está cerrada.");
        if (req.ExtraMinutes <= 0) throw new AppException("Los minutos deben ser mayores que cero.");

        var station = session.Station ?? await _db.Stations.FirstAsync(s => s.Id == session.StationId);
        var amount = Math.Round(session.RatePerHour * req.ExtraMinutes / 60m, 2, MidpointRounding.AwayFromZero);

        await using var tx = await _db.Database.BeginTransactionAsync();

        session.PlannedMinutes += req.ExtraMinutes;
        if (req.ChargeUpfront && amount > 0)
        {
            var cash = await _cash.GetOpenAsync()
                       ?? throw new AppException("Debe abrir la caja para cobrar la extensión.", 409);
            session.AmountCharged = Math.Round(session.AmountCharged + amount, 2);
            await _cash.AddMovementAsync(cash.Id, CashMovementType.TimeSale, req.Method, amount,
                $"Extensión {req.ExtraMinutes} min - {station.Name}", userId, sessionId: session.Id);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await NotifyStationAsync(station, session);
        var consumption = await ConsumptionAsync(session.Id);
        return Mappers.ToDto(session, consumption, DateTime.UtcNow);
    }

    public async Task<SessionDto> PauseAsync(int sessionId, bool pause, int userId)
    {
        var session = await _db.Sessions.Include(s => s.Station)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw AppException.NotFound("Sesión");
        if (session.Status is SessionStatus.Closed or SessionStatus.Cancelled)
            throw new AppException("La sesión está cerrada.");

        var station = session.Station ?? await _db.Stations.FirstAsync(s => s.Id == session.StationId);

        if (pause && session.Status == SessionStatus.Active)
        {
            session.Status = SessionStatus.Paused;
            session.PausedAtUtc = DateTime.UtcNow;
            station.Status = StationStatus.Paused;
        }
        else if (!pause && session.Status == SessionStatus.Paused)
        {
            if (session.PausedAtUtc is DateTime p)
                session.PausedSeconds += (int)(DateTime.UtcNow - p).TotalSeconds;
            session.PausedAtUtc = null;
            session.Status = SessionStatus.Active;
            station.Status = StationStatus.InUse;
        }

        await _db.SaveChangesAsync();
        await NotifyStationAsync(station, session);
        var consumption = await ConsumptionAsync(session.Id);
        return Mappers.ToDto(session, consumption, DateTime.UtcNow);
    }

    public async Task<SessionDto> TransferAsync(TransferSessionRequest req, int userId)
    {
        var session = await _db.Sessions.Include(s => s.Station)
            .FirstOrDefaultAsync(s => s.Id == req.SessionId)
            ?? throw AppException.NotFound("Sesión");
        if (session.Status is SessionStatus.Closed or SessionStatus.Cancelled)
            throw new AppException("La sesión está cerrada.");

        var target = await _db.Stations.FirstOrDefaultAsync(s => s.Id == req.TargetStationId)
                     ?? throw AppException.NotFound("Equipo destino");
        if (target.Status is StationStatus.InUse or StationStatus.Paused)
            throw new AppException("El equipo destino está ocupado.");

        var origin = session.Station ?? await _db.Stations.FirstAsync(s => s.Id == session.StationId);

        await using var tx = await _db.Database.BeginTransactionAsync();
        origin.Status = StationStatus.Available;
        origin.CurrentSessionId = null;
        target.Status = session.Status == SessionStatus.Paused ? StationStatus.Paused : StationStatus.InUse;
        target.CurrentSessionId = session.Id;
        session.StationId = target.Id;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _realtime.SendStationCommandAsync(origin.Id, AgentCommandType.Lock);
        await _realtime.SendAgentCommandAsync(origin.Id, AgentCommandType.Lock);
        await _realtime.SendAgentCommandAsync(target.Id, AgentCommandType.Unlock);
        await _realtime.SendStationCommandAsync(target.Id, AgentCommandType.Unlock);
        await NotifyStationAsync(target, session);

        var consumption = await ConsumptionAsync(session.Id);
        return Mappers.ToDto(session, consumption, DateTime.UtcNow);
    }

    public async Task<List<SessionDto>> ActiveAsync()
    {
        var sessions = await _db.Sessions.AsNoTracking()
            .Include(s => s.Station).Include(s => s.Customer)
            .Where(s => s.Status != SessionStatus.Closed && s.Status != SessionStatus.Cancelled)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var result = new List<SessionDto>();
        foreach (var s in sessions)
            result.Add(Mappers.ToDto(s, await ConsumptionAsync(s.Id), now));
        return result;
    }

    public async Task<SessionDto> GetAsync(int id)
    {
        var s = await _db.Sessions.AsNoTracking().Include(x => x.Station).Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw AppException.NotFound("Sesión");
        return Mappers.ToDto(s, await ConsumptionAsync(s.Id), DateTime.UtcNow);
    }

    private async Task NotifyStationAsync(Station station, Session session)
    {
        var elapsed = SessionCalculator.ElapsedMinutes(session, DateTime.UtcNow);
        var amount = SessionCalculator.TimeAmount(session, elapsed)
                     + await ConsumptionAsync(session.Id);

        await _realtime.StationChangedAsync(new StationStateEvent(
            station.Id, station.Status,
            session.Status is SessionStatus.Closed or SessionStatus.Cancelled ? null : session.Id,
            elapsed, SessionCalculator.RemainingMinutes(session, elapsed),
            Math.Round(amount, 2), SessionCalculator.IsOvertime(session, elapsed)));
    }
}
