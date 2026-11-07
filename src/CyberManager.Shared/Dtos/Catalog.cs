namespace CyberManager.Shared.Dtos;

public record ZoneDto(int Id, string Name, string Color, int SortOrder, bool Active);
public record SaveZoneRequest(string Name, string Color, int SortOrder, bool Active);

public record RateDto(int Id, string Name, decimal PricePerHour, int MinimumMinutes, int BillingBlockMinutes, bool Active);
public record SaveRateRequest(string Name, decimal PricePerHour, int MinimumMinutes, int BillingBlockMinutes, bool Active);

public record TimePackageDto(int Id, string Name, int Minutes, int BonusMinutes, decimal Price, bool Active);
public record SaveTimePackageRequest(string Name, int Minutes, int BonusMinutes, decimal Price, bool Active);

public record StationDto(
    int Id, string Name, string? IpAddress, string? MacAddress, int? ZoneId, string? ZoneName, string? ZoneColor,
    int? RateId, string? RateName, decimal RatePerHour,
    StationStatus Status, int? CurrentSessionId, DateTime? LastSeenUtc, string? Notes, int SortOrder,
    bool Active, string? CurrentUser, string? Version, StationKind Kind = StationKind.Pc,
    string? Platform = null, int? Controllers = null);

public record SaveStationRequest(
    string Name, string? IpAddress, string? MacAddress, int? ZoneId, int? RateId,
    string? Notes, int SortOrder, bool Active, StationKind Kind = StationKind.Pc,
    string? Platform = null, int? Controllers = null);

public record StationCommandRequest(AgentCommandType Command, string? Argument);

/// <summary>Orden manual de los equipos según la posición de cada ID en la lista.</summary>
public record ReorderStationsRequest(List<int> StationIds);

public record CategoryDto(int Id, string Name, string Color, int SortOrder, bool Active);
public record SaveCategoryRequest(string Name, string Color, int SortOrder, bool Active);

public record ProductDto(
    int Id, string Sku, string Name, int? CategoryId, string? CategoryName, string? CategoryColor,
    decimal Price, decimal Cost, decimal Stock, decimal MinStock, string Unit, bool IsService,
    decimal TaxRate, bool Active, bool LowStock);

public record SaveProductRequest(
    string Sku, string Name, int? CategoryId, decimal Price, decimal Cost, decimal Stock,
    decimal MinStock, string Unit, bool IsService, decimal TaxRate, bool Active);

public record SupplierDto(int Id, string Name, string? Phone, string? Email, string? Address, string? Notes, bool Active);
public record SaveSupplierRequest(string Name, string? Phone, string? Email, string? Address, string? Notes, bool Active);

public record SupplierBalanceDto(int SupplierId, string SupplierName, decimal Purchases, decimal Payments, decimal Balance);
public record SupplierPaymentDto(long Id, int SupplierId, string SupplierName, DateTime DateUtc, decimal Amount,
    PaymentMethod Method, string? Reference, string? Notes);
public record CreateSupplierPaymentRequest(decimal Amount, PaymentMethod Method, string? Reference, string? Notes);

public record CustomerDto(
    int Id, string Name, string? Phone, string? Email, string? DocumentId, string? Address, string? FiscalData,
    decimal Balance, decimal CreditLimit, bool IsMember, bool Active, string? Notes, DateTime CreatedUtc);

public record SaveCustomerRequest(
    string Name, string? Phone, string? Email, string? DocumentId, string? Address, string? FiscalData,
    decimal CreditLimit, bool IsMember, bool Active, string? Notes);

public record RechargeRequest(decimal Amount, PaymentMethod Method, string? Description);
public record CustomerTransactionDto(long Id, int CustomerId, CustomerTransactionType Type, decimal Amount,
    decimal BalanceAfter, string? Description, DateTime CreatedUtc);
