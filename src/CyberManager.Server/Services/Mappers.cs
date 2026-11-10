using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Services;

public static class Mappers
{
    public static StationDto ToDto(Station s) => new(
        s.Id, s.Name, s.IpAddress, s.MacAddress, s.ZoneId, s.Zone?.Name, s.Zone?.Color,
        s.RateId, s.Rate?.Name, s.Rate?.PricePerHour ?? 0,
        s.Status, s.CurrentSessionId, s.LastSeenUtc, s.Notes, s.SortOrder,
        s.Active, s.CurrentUser, s.Version, s.Kind, s.Platform, s.Controllers);

    public static ProductDto ToDto(Product p) => new(
        p.Id, p.Sku, p.Name, p.CategoryId, p.Category?.Name, p.Category?.Color,
        p.Price, p.Cost, p.Stock, p.MinStock, p.Unit, p.IsService, p.TaxRate,
        p.Active, p.Stock <= p.MinStock, p.Icon);

    public static CustomerDto ToDto(Customer c) => new(
        c.Id, c.Name, c.Phone, c.Email, c.DocumentId, c.Address, c.FiscalData,
        c.Balance, c.CreditLimit, c.IsMember, c.Active, c.Notes, c.CreatedUtc);

    public static SaleItemDto ToDto(SaleItem i) => new(
        i.Id, i.ProductId, i.Description, i.Quantity, i.UnitPrice, i.Discount, i.Total);

    public static SessionDto ToDto(Session s, decimal consumption, DateTime nowUtc)
    {
        var elapsed = SessionCalculator.ElapsedMinutes(s, nowUtc);
        var timeAmount = SessionCalculator.TimeAmount(s, elapsed);
        var remaining = SessionCalculator.RemainingMinutes(s, elapsed);
        var overtime = SessionCalculator.IsOvertime(s, elapsed);
        var total = Math.Round(timeAmount + consumption, 2);

        return new SessionDto(s.Id, s.StationId, s.Station?.Name ?? string.Empty, s.CustomerId,
            s.Customer?.Name, s.Type, s.Status, s.StartedUtc, s.EndedUtc, s.PausedSeconds,
            s.PlannedMinutes, elapsed, remaining, s.RatePerHour, timeAmount, consumption, total,
            overtime, s.Notes, s.CashSessionId);
    }
}
