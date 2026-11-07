using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public class CashService
{
    private readonly Data.AppDbContext _db;
    private readonly ExchangeRateService _rates;
    public CashService(Data.AppDbContext db, ExchangeRateService rates)
    {
        _db = db;
        _rates = rates;
    }

    public async Task<CashSession?> GetOpenAsync()
        => await _db.CashSessions
            .Include(x => x.Movements)
            .Where(x => x.IsOpen)
            .OrderByDescending(x => x.OpenedUtc)
            .FirstOrDefaultAsync();

    public async Task<CashSessionDto> GetCurrentAsync()
    {
        var cs = await GetOpenAsync() ?? throw new AppException("No hay una caja abierta.", 409);
        return await ToDtoAsync(cs);
    }

    public async Task<CashSessionDto> OpenAsync(CashOpenRequest req, int userId)
    {
        if (await _db.CashSessions.AnyAsync(x => x.IsOpen))
            throw new AppException("Ya existe una caja abierta. Cierre el turno actual primero.", 409);

        var rate = req.ExchangeRate ?? await _rates.GetRateAsync();
        if (rate <= 0) throw new AppException("Debe registrar la tasa del día antes de abrir la caja.");

        var cs = new CashSession
        {
            OpenedByUserId = userId,
            OpenedUtc = DateTime.UtcNow,
            OpeningAmount = req.OpeningAmount,
            OpeningExchangeRate = rate,
            IsOpen = true,
            Notes = req.Notes
        };
        _db.CashSessions.Add(cs);
        await _db.SaveChangesAsync();

        _db.CashMovements.Add(new CashMovement
        {
            CashSessionId = cs.Id,
            Type = CashMovementType.Opening,
            PaymentMethod = PaymentMethod.Cash,
            Amount = req.OpeningAmount,
            ExchangeRate = rate,
            AmountVes = Money.ToVes(req.OpeningAmount, rate),
            Description = "Fondo inicial de caja",
            UserId = userId
        });
        await _db.SaveChangesAsync();

        return await ToDtoAsync(cs);
    }

    public async Task<CashMovement> AddMovementAsync(int cashSessionId, CashMovementType type, PaymentMethod method,
        decimal amountUsd, string description, int? userId, long? saleId = null, int? sessionId = null, decimal? rate = null)
    {
        if (amountUsd == 0) throw new AppException("El monto del movimiento no puede ser cero.");
        var r = rate ?? await _rates.GetRateAsync();
        var m = new CashMovement
        {
            CashSessionId = cashSessionId,
            Type = type,
            PaymentMethod = method,
            Amount = amountUsd,
            ExchangeRate = r,
            AmountVes = Money.ToVes(amountUsd, r),
            Description = description,
            UserId = userId,
            SaleId = saleId,
            SessionId = sessionId
        };
        _db.CashMovements.Add(m);
        await _db.SaveChangesAsync();
        return m;
    }

    public async Task<CashSessionDto> AddManualMovementAsync(CashMovementRequest req, int userId)
    {
        var cs = await GetOpenAsync() ?? throw new AppException("No hay una caja abierta.", 409);
        if (req.Amount <= 0) throw new AppException("El monto debe ser mayor que cero.");

        var signed = req.Type switch
        {
            CashMovementType.Withdrawal or CashMovementType.Expense or CashMovementType.Refund => -req.Amount,
            _ => req.Amount
        };

        await AddMovementAsync(cs.Id, req.Type, req.Method, signed, req.Description, userId);
        return await ToDtoAsync(cs);
    }

    public async Task<CashSessionDto> CloseAsync(CashCloseRequest req, int userId)
    {
        var cs = await GetOpenAsync() ?? throw new AppException("No hay una caja abierta.", 409);
        var dto = await ToDtoAsync(cs);

        cs.ClosingAmount = req.ClosingAmount;
        cs.ClosingExchangeRate = await _rates.GetRateAsync();
        cs.ExpectedAmount = dto.ExpectedAmount;
        cs.Difference = Math.Round(req.ClosingAmount - dto.ExpectedAmount, 2);
        cs.ClosedUtc = DateTime.UtcNow;
        cs.ClosedByUserId = userId;
        cs.IsOpen = false;
        cs.Notes = req.Notes ?? cs.Notes;
        await _db.SaveChangesAsync();

        return await ToDtoAsync(cs);
    }

    public async Task<List<CashReportRow>> HistoryAsync(DateTime from, DateTime to)
        => await _db.CashSessions.AsNoTracking()
            .Where(x => x.OpenedUtc >= from && x.OpenedUtc <= to)
            .OrderByDescending(x => x.OpenedUtc)
            .Select(x => new CashReportRow(x.Id, x.OpenedUtc, x.ClosedUtc,
                _db.Users.Where(u => u.Id == x.OpenedByUserId).Select(u => u.FullName).FirstOrDefault(),
                x.OpeningAmount, x.ExpectedAmount - x.OpeningAmount, 0, x.ExpectedAmount,
                x.ClosingAmount, x.Difference, x.OpeningExchangeRate, x.ClosingExchangeRate))
            .ToListAsync();

    public async Task<CashSessionDto> ToDtoAsync(CashSession cs)
    {
        var movements = cs.Movements.Any()
            ? cs.Movements
            : await _db.CashMovements.AsNoTracking().Where(m => m.CashSessionId == cs.Id).ToListAsync();

        decimal Sum(CashMovementType t, params PaymentMethod[] methods) =>
            movements.Where(m => m.Type == t && (methods.Length == 0 || methods.Contains(m.PaymentMethod)))
                     .Sum(m => m.Amount);

        var salesCash = Sum(CashMovementType.Sale, PaymentMethod.Cash) + Sum(CashMovementType.TimeSale, PaymentMethod.Cash);
        var salesCashUsd = Sum(CashMovementType.Sale, PaymentMethod.CashUsd) + Sum(CashMovementType.TimeSale, PaymentMethod.CashUsd);
        var salesCard = Sum(CashMovementType.Sale, PaymentMethod.Card) + Sum(CashMovementType.TimeSale, PaymentMethod.Card);
        var salesTransfer = Sum(CashMovementType.Sale, PaymentMethod.Transfer)
                            + Sum(CashMovementType.TimeSale, PaymentMethod.Transfer)
                            + Sum(CashMovementType.Sale, PaymentMethod.PagoMovil)
                            + Sum(CashMovementType.TimeSale, PaymentMethod.PagoMovil);
        var deposits = Sum(CashMovementType.Deposit);
        var withdrawals = -Sum(CashMovementType.Withdrawal);
        var expenses = -Sum(CashMovementType.Expense);
        var refunds = -Sum(CashMovementType.Refund);

        var expected = Math.Round(cs.OpeningAmount + salesCash + salesCashUsd + deposits - withdrawals - expenses - refunds, 2);
        var openerName = await _db.Users.AsNoTracking().Where(u => u.Id == cs.OpenedByUserId)
            .Select(u => u.FullName).FirstOrDefaultAsync();

        return new CashSessionDto(cs.Id, cs.OpenedByUserId, openerName, cs.OpenedUtc, cs.ClosedUtc,
            cs.OpeningAmount, cs.OpeningExchangeRate, cs.ClosingExchangeRate,
            salesCash, salesCashUsd, salesCard, salesTransfer, deposits, withdrawals, expenses, refunds, expected,
            cs.ClosingAmount, cs.Difference, cs.IsOpen, cs.Notes,
            movements.OrderBy(m => m.CreatedUtc).Select(m => new CashMovementDto(
                m.Id, m.Type, m.PaymentMethod, m.Amount, m.AmountVes, m.Description, m.CreatedUtc)).ToList());
    }
}
