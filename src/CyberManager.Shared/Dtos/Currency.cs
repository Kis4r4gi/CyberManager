namespace CyberManager.Shared.Dtos;

public record ExchangeRateDto(int Id, DateTime Date, decimal Rate, RateSource Source, bool IsActive,
    DateTime CreatedUtc, string? Notes);

public record SetExchangeRateRequest(DateTime Date, decimal Rate, RateSource Source, string? Notes);

/// <summary>Resultado de consultar la tasa oficial del BCV.</summary>
public record BcvRateDto(int Id, DateTime Date, decimal Rate, RateSource Source, bool IsActive,
    DateTime CreatedUtc, string? Notes, DateTime ValueDate, bool Updated);

public record MoneyDto(decimal Usd, decimal Ves);

/// <summary>Datos completos para reimprimir un comprobante.</summary>
public record InvoiceDto(
    string BusinessName, string? BusinessTaxId, string? BusinessAddress, string? BusinessPhone,
    string Number, string? ControlNumber, InvoiceType Type, DateTime CreatedUtc,
    string? CustomerName, string? CustomerTaxId, decimal ExchangeRate,
    List<InvoiceLineDto> Lines,
    decimal Subtotal, decimal Discount, decimal Tax, decimal Igtf, decimal Total,
    decimal SubtotalVes, decimal TaxVes, decimal IgtfVes, decimal TotalVes,
    decimal PaidAmount, Currency PaidCurrency, decimal Change, PaymentMethod Method,
    string? CashierName, string? Footer);

public record InvoiceLineDto(string Description, decimal Quantity, decimal UnitPrice, decimal Total);
