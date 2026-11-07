namespace CyberManager.Shared.Domain;

/// <summary>Turno de caja de un cajero.</summary>
public class CashSession
{
    public int Id { get; set; }
    public int OpenedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public DateTime OpenedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedUtc { get; set; }
    public decimal OpeningAmount { get; set; }
    public decimal OpeningExchangeRate { get; set; }
    public decimal? ClosingExchangeRate { get; set; }
    public decimal? ClosingAmount { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal Difference { get; set; }
    public bool IsOpen { get; set; } = true;
    public string? Notes { get; set; }
    public List<CashMovement> Movements { get; set; } = new();
}

public class CashMovement
{
    public long Id { get; set; }
    public int CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }
    public CashMovementType Type { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    /// <summary>Importe en moneda base (USD).</summary>
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal AmountVes { get; set; }
    public string Description { get; set; } = string.Empty;
    public long? SaleId { get; set; }
    public int? SessionId { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class Sale
{
    public long Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int? CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }
    public int? UserId { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? StationId { get; set; }
    public Station? Station { get; set; }
    public int? SessionId { get; set; }
    public Session? Session { get; set; }
    /// <summary>Importes en moneda base (USD).</summary>
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal IgtfAmount { get; set; }
    public decimal Total { get; set; }
    /// <summary>Tasa Bs/USD aplicada al momento de la venta.</summary>
    public decimal ExchangeRate { get; set; }
    public decimal SubtotalVes { get; set; }
    public decimal TaxVes { get; set; }
    public decimal IgtfAmountVes { get; set; }
    public decimal TotalVes { get; set; }
    /// <summary>Monto entregado por el cliente en la moneda de pago.</summary>
    public decimal PaidAmount { get; set; }
    public Currency PaidCurrency { get; set; } = Currency.Ves;
    /// <summary>Vuelto en la moneda de pago.</summary>
    public decimal Change { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public InvoiceType InvoiceType { get; set; } = InvoiceType.Ticket;
    public string? CustomerName { get; set; }
    public string? CustomerTaxId { get; set; }
    public string? ControlNumber { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledUtc { get; set; }
    public List<SaleItem> Items { get; set; } = new();
}

public class SaleItem
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Total { get; set; }
}
