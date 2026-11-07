using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public class InvoiceService
{
    private readonly Data.AppDbContext _db;
    private readonly SettingsService _settings;

    public InvoiceService(Data.AppDbContext db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<InvoiceDto> BuildAsync(long saleId)
    {
        var s = await _db.Sales.AsNoTracking().Include(x => x.Items).Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == saleId)
            ?? throw AppException.NotFound("Venta");

        var cfg = await _settings.GetAllAsync();
        string Get(string key, string fallback = "") => cfg.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

        var cashier = s.UserId is int uid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).FirstOrDefaultAsync()
            : null;

        return new InvoiceDto(
            Get(SettingsKeys.BusinessName, "CyberManager"),
            Get(SettingsKeys.BusinessTaxId),
            Get(SettingsKeys.BusinessAddress),
            Get(SettingsKeys.BusinessPhone),
            s.Number, s.ControlNumber, s.InvoiceType, s.CreatedUtc,
            s.CustomerName ?? s.Customer?.Name, s.CustomerTaxId,
            s.ExchangeRate,
            s.Items.Select(i => new InvoiceLineDto(i.Description, i.Quantity, i.UnitPrice, i.Total)).ToList(),
            s.Subtotal, s.Discount, s.Tax, s.IgtfAmount, s.Total,
            s.SubtotalVes, s.TaxVes, s.IgtfAmountVes, s.TotalVes,
            s.PaidAmount, s.PaidCurrency, s.Change, s.PaymentMethod,
            cashier, Get(SettingsKeys.TicketFooter, "¡Gracias por su visita!"));
    }
}
