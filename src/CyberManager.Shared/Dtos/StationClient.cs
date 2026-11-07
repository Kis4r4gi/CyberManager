namespace CyberManager.Shared.Dtos;

public record StationClientStateDto(
    int StationId, string Name, string? ZoneName, bool Locked, StationStatus Status,
    SessionDto? Session, decimal ExchangeRate, string BusinessName, string? LocalCurrencySymbol,
    string? Footer, DateTime ServerTimeUtc, WallpaperPlaylistDto? Wallpaper = null,
    PromoVideoPlaylistDto? Promos = null);

public record StationOrderItem(int ProductId, decimal Quantity);
public record StationOrderRequest(List<StationOrderItem> Items);

public record StationConfigDto(int StationId, string Name, string ServerUrl, string Token);
