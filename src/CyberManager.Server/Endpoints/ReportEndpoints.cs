using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports").WithTags("Reportes").RequireAuthorization();

        g.MapGet("/summary", async (DateTime? from, DateTime? to, ReportService reports) =>
            Results.Ok(await reports.SummaryAsync(Range(from, to).From, Range(from, to).To)));

        g.MapGet("/sales", async (DateTime? from, DateTime? to, ReportService reports) =>
            Results.Ok(await reports.SalesByDayAsync(Range(from, to).From, Range(from, to).To)));

        g.MapGet("/products", async (DateTime? from, DateTime? to, ReportService reports) =>
            Results.Ok(await reports.ProductsAsync(Range(from, to).From, Range(from, to).To)));

        g.MapGet("/stations", async (DateTime? from, DateTime? to, ReportService reports) =>
            Results.Ok(await reports.StationsAsync(Range(from, to).From, Range(from, to).To)));

        g.MapGet("/inventory", async (ReportService reports) => Results.Ok(await reports.InventoryAsync()));

        g.MapGet("/daily-closing", async (DateTime? date, ReportService reports) =>
            Results.Ok(await reports.DailyClosingAsync(date ?? DateTime.Today)));

        g.MapGet("/cash", async (DateTime? from, DateTime? to, CashService cash) =>
            Results.Ok(await cash.HistoryAsync(Range(from, to).From, Range(from, to).To)));

        return app;
    }

    private static (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var toDate = (to ?? DateTime.Today).Date.AddDays(1);
        var fromDate = (from ?? DateTime.Today.AddDays(-6)).Date;
        return (fromDate.ToUniversalTime(), toDate.ToUniversalTime());
    }
}
