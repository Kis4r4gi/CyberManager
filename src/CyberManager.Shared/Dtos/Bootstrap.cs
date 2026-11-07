namespace CyberManager.Shared.Dtos;

/// <summary>Catálogo completo necesario para operar la caja sin múltiples llamadas.</summary>
public record BootstrapDto(
    List<ZoneDto> Zones,
    List<RateDto> Rates,
    List<TimePackageDto> Packages,
    List<CategoryDto> Categories,
    List<ProductDto> Products,
    Dictionary<string, string> Settings,
    ExchangeRateDto? ExchangeRate);
