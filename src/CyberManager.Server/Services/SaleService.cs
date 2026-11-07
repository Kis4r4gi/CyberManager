using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public class SaleService
{
    private readonly Data.AppDbContext _db;
    private readonly ExchangeRateService _rates;
    private readonly SettingsService _settings;
    private readonly CashService _cash;
    private readonly StockService _stock;

    public SaleService(Data.AppDbContext db, ExchangeRateService rates, SettingsService settings,
        CashService cash, StockService stock)
    {
        _db = db;
        _rates = rates;
        _settings = settings;
        _cash = cash;
        _stock = stock;
    }

    public async Task<SaleDto> CreateAsync(CreateSaleRequest req, int? userId)
    {
        if (req.Items is null || req.Items.Count == 0)
            throw new AppException("La venta no tiene artículos.");

        var rate = req.ExchangeRate ?? await _rates.GetRateAsync();
        if (rate <= 0)
            throw new AppException("Debe registrar la tasa de cambio del día antes de vender.");

        var igtfRate = await _settings.GetBoolAsync(SettingsKeys.IgtfEnabled, true)
            ? await _settings.GetDecimalAsync(SettingsKeys.IgtfRate, 3m)
            : 0m;

        // ---- líneas e importes (precios con IVA incluido) ----
        decimal gross = 0, net = 0, tax = 0;
        var lines = new List<SaleItem>();
        var stockNeeded = new Dictionary<int, decimal>();

        foreach (var item in req.Items)
        {
            if (item.Quantity <= 0) throw new AppException("La cantidad debe ser mayor que cero.");
            if (item.UnitPrice < 0) throw new AppException("El precio no puede ser negativo.");

            Product? product = null;
            if (item.ProductId is int pid)
            {
                product = await _db.Products.FirstOrDefaultAsync(p => p.Id == pid)
                          ?? throw AppException.NotFound("Producto");
                if (!product.Active) throw new AppException($"El producto '{product.Name}' está inactivo.");
                if (!product.IsService)
                    stockNeeded[pid] = stockNeeded.GetValueOrDefault(pid) + item.Quantity;
            }

            var lineGross = Math.Round(item.Quantity * item.UnitPrice - item.Discount, 2, MidpointRounding.AwayFromZero);
            var lineTax = item.TaxRate > 0
                ? Math.Round(lineGross * item.TaxRate / (100m + item.TaxRate), 2, MidpointRounding.AwayFromZero)
                : 0m;

            gross += lineGross;
            net += lineGross - lineTax;
            tax += lineTax;

            lines.Add(new SaleItem
            {
                ProductId = item.ProductId,
                Description = string.IsNullOrWhiteSpace(item.Description)
                    ? product?.Name ?? "Artículo"
                    : item.Description!,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                TaxRate = item.TaxRate,
                Total = lineGross
            });
        }

        var orderDiscount = Math.Max(0, req.Discount);
        var finalTotal = Math.Round(Math.Max(0, gross - orderDiscount), 2, MidpointRounding.AwayFromZero);
        var scale = gross > 0 ? finalTotal / gross : 0;
        net = Math.Round(net * scale, 2, MidpointRounding.AwayFromZero);
        tax = Math.Round(tax * scale, 2, MidpointRounding.AwayFromZero);

        var paidCurrency = req.PaidCurrency
            ?? (req.Method == PaymentMethod.CashUsd ? Currency.Usd : Currency.Ves);
        if (paidCurrency == Currency.Usd && req.Method != PaymentMethod.CashUsd)
            paidCurrency = Currency.Ves;

        var igtf = paidCurrency == Currency.Usd
            ? Math.Round(finalTotal * igtfRate / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var grandTotal = Math.Round(finalTotal + igtf, 2, MidpointRounding.AwayFromZero);
        var due = paidCurrency == Currency.Usd ? grandTotal : Money.ToVes(grandTotal, rate);

        // ---- validaciones de stock e inventario ----
        foreach (var (pid, qty) in stockNeeded)
        {
            var p = await _db.Products.AsNoTracking().FirstAsync(x => x.Id == pid);
            if (p.Stock < qty)
                throw new AppException($"Stock insuficiente de '{p.Name}'. Disponible: {p.Stock}, solicitado: {qty}.");
        }

        // ---- caja ----
        CashSession? cash = null;
        if (req.Method != PaymentMethod.Pending)
        {
            cash = await _cash.GetOpenAsync()
                   ?? throw new AppException("Debe abrir la caja antes de registrar ventas.", 409);
        }

        var paid = req.PaidAmount;
        if (req.Method is PaymentMethod.Card or PaymentMethod.Transfer or PaymentMethod.PagoMovil
            or PaymentMethod.CustomerBalance)
            paid = due;
        if (paid <= 0) paid = due;
        var change = Math.Max(0, Math.Round(paid - due, 2, MidpointRounding.AwayFromZero));

        Customer? customer = null;
        if (req.CustomerId is int cid)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == cid)
                       ?? throw AppException.NotFound("Cliente");
        }

        if (req.Method == PaymentMethod.CustomerBalance)
        {
            if (customer is null) throw new AppException("Seleccione un cliente para cobrar a su cuenta.");
            if (customer.Balance < grandTotal)
                throw new AppException($"Saldo insuficiente. Disponible: {customer.Balance:F2} USD.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var sale = new Sale
        {
            Number = Guid.NewGuid().ToString("N")[..12],
            CashSessionId = cash?.Id,
            UserId = userId,
            CustomerId = req.CustomerId,
            StationId = req.StationId,
            SessionId = req.SessionId,
            Subtotal = net,
            Discount = Math.Round(orderDiscount, 2),
            Tax = tax,
            IgtfAmount = igtf,
            Total = grandTotal,
            ExchangeRate = rate,
            SubtotalVes = Money.ToVes(net, rate),
            TaxVes = Money.ToVes(tax, rate),
            IgtfAmountVes = Money.ToVes(igtf, rate),
            TotalVes = Money.ToVes(grandTotal, rate),
            PaidAmount = Math.Round(paid, 2),
            PaidCurrency = paidCurrency,
            Change = change,
            PaymentMethod = req.Method,
            InvoiceType = req.InvoiceType,
            Status = SaleStatus.Completed,
            Notes = req.Notes,
            CreatedUtc = DateTime.UtcNow,
            Items = lines
        };
        sale.CustomerName = customer?.Name;
        sale.CustomerTaxId = req.CustomerTaxId ?? customer?.DocumentId;

        if (req.InvoiceType == InvoiceType.Factura)
            sale.ControlNumber = await NextControlNumberAsync(InvoiceType.Factura);

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();
        sale.Number = $"V-{sale.Id:D6}";
        await _db.SaveChangesAsync();

        // ---- inventario ----
        foreach (var item in lines.Where(i => i.ProductId.HasValue))
        {
            var product = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == item.ProductId!.Value);
            if (product.IsService) continue;
            await _stock.ApplyAsync(product.Id, StockMovementType.Sale, -item.Quantity, product.Cost,
                sale.Number, userId, $"Venta {sale.Number}");
        }

        // ---- cobros ----
        if (req.Method == PaymentMethod.CustomerBalance && customer is not null)
        {
            customer.Balance = Math.Round(customer.Balance - grandTotal, 2);
            _db.CustomerTransactions.Add(new CustomerTransaction
            {
                CustomerId = customer.Id,
                Type = CustomerTransactionType.Consumption,
                Amount = -grandTotal,
                BalanceAfter = customer.Balance,
                Description = $"Consumo venta {sale.Number}",
                UserId = userId
            });
            await _db.SaveChangesAsync();
        }
        else if ((req.Method is PaymentMethod.Cash or PaymentMethod.CashUsd
                 or PaymentMethod.Card or PaymentMethod.Transfer or PaymentMethod.PagoMovil) && cash is not null)
        {
            await _cash.AddMovementAsync(cash.Id, CashMovementType.Sale, req.Method, grandTotal,
                $"Venta {sale.Number}", userId, sale.Id, rate: rate);
        }

        await tx.CommitAsync();
        return await ToDtoAsync(sale.Id);
    }

    public async Task<SaleDto> CancelAsync(long saleId, int userId)
    {
        var sale = await _db.Sales.Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == saleId)
            ?? throw AppException.NotFound("Venta");

        if (sale.Status == SaleStatus.Cancelled)
            throw new AppException("La venta ya está anulada.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var item in sale.Items.Where(i => i.ProductId.HasValue))
        {
            var product = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == item.ProductId!.Value);
            if (product.IsService) continue;
            await _stock.ApplyAsync(product.Id, StockMovementType.Return, item.Quantity, product.Cost,
                sale.Number, userId, $"Anulación venta {sale.Number}");
        }

        if (sale.PaymentMethod == PaymentMethod.CustomerBalance && sale.CustomerId is int cid)
        {
            var customer = await _db.Customers.FirstAsync(c => c.Id == cid);
            customer.Balance = Math.Round(customer.Balance + sale.Total, 2);
            _db.CustomerTransactions.Add(new CustomerTransaction
            {
                CustomerId = cid,
                Type = CustomerTransactionType.Refund,
                Amount = sale.Total,
                BalanceAfter = customer.Balance,
                Description = $"Anulación venta {sale.Number}",
                UserId = userId
            });
        }
        else if (sale.PaymentMethod is PaymentMethod.Cash or PaymentMethod.CashUsd
                 or PaymentMethod.Card or PaymentMethod.Transfer or PaymentMethod.PagoMovil)
        {
            var cash = await _cash.GetOpenAsync();
            if (cash is not null)
                await _cash.AddMovementAsync(cash.Id, CashMovementType.Refund, sale.PaymentMethod, -sale.Total,
                    $"Anulación venta {sale.Number}", userId, sale.Id, rate: sale.ExchangeRate);
        }

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await ToDtoAsync(sale.Id);
    }

    public async Task<SaleDto> ToDtoAsync(long saleId)
    {
        var s = await _db.Sales.AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Station)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == saleId)
            ?? throw AppException.NotFound("Venta");

        return new SaleDto(s.Id, s.Number, s.StationId, s.Station?.Name, s.SessionId,
            s.CustomerId, s.CustomerName ?? s.Customer?.Name,
            s.Subtotal, s.Discount, s.Tax, s.IgtfAmount, s.Total, s.ExchangeRate, s.TotalVes,
            s.PaidAmount, s.PaidCurrency, s.Change, s.PaymentMethod, s.InvoiceType, s.ControlNumber,
            s.Status, s.CreatedUtc, s.Notes,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.Description, i.Quantity,
                i.UnitPrice, i.Discount, i.Total)).ToList());
    }

    public async Task<List<SaleDto>> ListAsync(DateTime from, DateTime to, int? stationId = null, int? take = 200)
    {
        var q = _db.Sales.AsNoTracking()
            .Include(x => x.Items).Include(x => x.Station).Include(x => x.Customer)
            .Where(x => x.CreatedUtc >= from && x.CreatedUtc <= to);

        if (stationId is int sid) q = q.Where(x => x.StationId == sid);

        var list = await q.OrderByDescending(x => x.CreatedUtc).Take(take ?? 200).ToListAsync();
        return list.Select(s => new SaleDto(s.Id, s.Number, s.StationId, s.Station?.Name, s.SessionId,
            s.CustomerId, s.CustomerName ?? s.Customer?.Name,
            s.Subtotal, s.Discount, s.Tax, s.IgtfAmount, s.Total, s.ExchangeRate, s.TotalVes,
            s.PaidAmount, s.PaidCurrency, s.Change, s.PaymentMethod, s.InvoiceType, s.ControlNumber,
            s.Status, s.CreatedUtc, s.Notes,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.Description, i.Quantity,
                i.UnitPrice, i.Discount, i.Total)).ToList())).ToList();
    }

    public async Task<string> NextControlNumberAsync(InvoiceType type)
    {
        var seq = await _db.InvoiceSequences.FirstOrDefaultAsync(x => x.Type == type && x.Active);
        if (seq is null)
        {
            seq = new InvoiceSequence { Type = type, Prefix = "00", NextNumber = 1, LastNumber = 99999999 };
            _db.InvoiceSequences.Add(seq);
        }
        if (seq.NextNumber > seq.LastNumber)
            throw new AppException("Se agotó la numeración de facturas. Configure un nuevo rango.");

        var number = seq.NextNumber;
        seq.NextNumber = number + 1;
        await _db.SaveChangesAsync();
        return $"{seq.Prefix}-{number:D8}";
    }
}
