namespace CyberManager.Shared.Dtos;

public record SessionDto(
    int Id, int StationId, string StationName, int? CustomerId, string? CustomerName,
    SessionType Type, SessionStatus Status, DateTime StartedUtc, DateTime? EndedUtc,
    int PausedSeconds, int PlannedMinutes, int ElapsedMinutes, int RemainingMinutes,
    decimal RatePerHour, decimal TimeAmount, decimal ConsumptionAmount, decimal TotalAmount,
    bool Overtime, string? Notes, int? CashSessionId);

public record StartSessionRequest(
    int StationId, SessionType Type, int PlannedMinutes, int? CustomerId,
    int? RateId, decimal? CustomRatePerHour, string? Notes, bool ChargeUpfront,
    decimal? UpfrontAmount = null, PaymentMethod UpfrontMethod = PaymentMethod.Cash);

public record StopSessionRequest(int SessionId, PaymentMethod Method, bool ChargeTime, string? Notes);
public record ExtendSessionRequest(int SessionId, int ExtraMinutes, bool ChargeUpfront, PaymentMethod Method);
public record TransferSessionRequest(int SessionId, int TargetStationId);
public record PauseSessionRequest(int SessionId, bool Pause);

public record SaleItemRequest(int? ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal Discount, decimal TaxRate);
public record CreateSaleRequest(
    List<SaleItemRequest> Items, PaymentMethod Method, decimal PaidAmount, decimal Discount,
    int? CustomerId, int? StationId, int? SessionId, string? Notes,
    InvoiceType InvoiceType = InvoiceType.Ticket, Currency? PaidCurrency = null,
    decimal? ExchangeRate = null, string? CustomerTaxId = null);

public record SaleItemDto(long Id, int? ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal Discount, decimal Total);
public record SaleDto(
    long Id, string Number, int? StationId, string? StationName, int? SessionId,
    int? CustomerId, string? CustomerName, decimal Subtotal, decimal Discount, decimal Tax,
    decimal IgtfAmount, decimal Total, decimal ExchangeRate, decimal TotalVes,
    decimal PaidAmount, Currency PaidCurrency, decimal Change, PaymentMethod Method,
    InvoiceType InvoiceType, string? ControlNumber, SaleStatus Status,
    DateTime CreatedUtc, string? Notes, List<SaleItemDto> Items);

public record CashOpenRequest(decimal OpeningAmount, decimal? ExchangeRate, string? Notes);
public record CashCloseRequest(decimal ClosingAmount, string? Notes);
public record CashMovementRequest(CashMovementType Type, PaymentMethod Method, decimal Amount, string Description);
public record CashMovementDto(long Id, CashMovementType Type, PaymentMethod Method, decimal Amount, decimal AmountVes, string Description, DateTime CreatedUtc);
public record CashSessionDto(
    int Id, int OpenedByUserId, string? OpenedByName, DateTime OpenedUtc, DateTime? ClosedUtc,
    decimal OpeningAmount, decimal OpeningExchangeRate, decimal? ClosingExchangeRate,
    decimal SalesCash, decimal SalesCashUsd, decimal SalesCard, decimal SalesTransfer,
    decimal Deposits, decimal Withdrawals, decimal Expenses, decimal Refunds, decimal ExpectedAmount,
    decimal? ClosingAmount, decimal Difference, bool IsOpen, string? Notes, List<CashMovementDto> Movements);

public record StockMovementRequest(int ProductId, StockMovementType Type, decimal Quantity, decimal UnitCost, string? Reference, string? Notes);
public record StockMovementDto(long Id, int ProductId, string ProductName, StockMovementType Type, decimal Quantity,
    decimal UnitCost, decimal BalanceAfter, string? Reference, string? Notes, DateTime CreatedUtc, string? UserName);

public record PurchaseItemRequest(int ProductId, decimal Quantity, decimal UnitCost);
public record CreatePurchaseRequest(int? SupplierId, string? InvoiceNumber, string? Notes, List<PurchaseItemRequest> Items);
public record PurchaseDto(long Id, int? SupplierId, string? SupplierName, string? InvoiceNumber, DateTime DateUtc,
    decimal Total, string? Notes, List<PurchaseItemRequest> Items);

public record ReservationDto(int Id, int StationId, string StationName, int? CustomerId, string CustomerName,
    string? Phone, DateTime StartUtc, DateTime EndUtc, ReservationStatus Status, string? Notes);
public record SaveReservationRequest(int StationId, int? CustomerId, string CustomerName, string? Phone,
    DateTime StartUtc, DateTime EndUtc, ReservationStatus Status, string? Notes);
