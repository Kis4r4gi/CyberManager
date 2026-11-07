namespace CyberManager.Shared.Dtos;

public record DashboardDto(
    int TotalStations, int OnlineStations, int AvailableStations, int InUseStations, int PausedStations,
    int MaintenanceStations, int ActiveSessions, decimal TodaySales, decimal TodayTimeSales,
    decimal TodayTotal, decimal TodayTotalVes, decimal ExchangeRate,
    decimal CashInDrawer, decimal CashInDrawerVes, int LowStockCount, int TodaySessions,
    int PendingReservations, decimal TodayConsumption, List<StationSummaryDto> Stations);

public record StationSummaryDto(int Id, string Name, string? ZoneName, StationStatus Status,
    int? SessionId, int ElapsedMinutes, int RemainingMinutes, decimal CurrentAmount,
    SessionType? SessionType, bool Overtime);

public record SalesReportRow(DateTime Date, int Count, decimal Products, decimal Time, decimal Total,
    decimal ProductsVes, decimal TimeVes, decimal TotalVes);
public record ProductReportRow(int ProductId, string Name, decimal Quantity, decimal Total, decimal TotalVes);
public record StationReportRow(int StationId, string Name, int Sessions, int Minutes, decimal TimeAmount,
    decimal Consumption, decimal TimeAmountVes, decimal ConsumptionVes);
public record InventoryReportRow(int ProductId, string Sku, string Name, decimal Stock, decimal MinStock, decimal Cost, decimal Price);
public record CashReportRow(int CashSessionId, DateTime OpenedUtc, DateTime? ClosedUtc, string? UserName,
    decimal OpeningAmount, decimal Sales, decimal Expenses, decimal Expected, decimal? Closing, decimal Difference,
    decimal OpeningExchangeRate, decimal? ClosingExchangeRate);

public record ReportRangeRequest(DateTime FromUtc, DateTime ToUtc);
public record ReportSummaryDto(decimal ProductSales, decimal TimeSales, decimal TotalSales, decimal TotalSalesVes,
    decimal Discounts, int SaleCount, int SessionCount, int SessionMinutes, decimal AverageTicket,
    decimal ExchangeRate);

public record PaymentBreakdownRow(PaymentMethod Method, int Count, decimal Total, decimal TotalVes);

public record DailyClosingDto(DateTime Date, string BusinessName, string BusinessTaxId,
    decimal ExchangeRate, decimal ProductSales, decimal TimeSales, decimal TotalSales, decimal TotalSalesVes,
    decimal Igtf, decimal Discounts, decimal AverageTicket, int SaleCount, int SessionCount, int SessionMinutes,
    decimal CashOpening, decimal CashExpected, decimal? CashCounted, decimal CashDifference,
    decimal CashExpenses, decimal SupplierPayments, decimal TotalExpenses,
    List<PaymentBreakdownRow> Payments, List<SupplierPaymentDto> SupplierPaymentsDetail,
    List<ProductReportRow> TopProducts);
