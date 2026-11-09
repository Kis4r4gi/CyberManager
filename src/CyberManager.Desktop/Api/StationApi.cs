using CyberManager.Shared.Dtos;

namespace CyberManager.Desktop.Api;

/// <summary>Cliente HTTP autenticado con el token del equipo (pantalla de estación).</summary>
public class StationApi : ApiClient
{
    public int StationId { get; }

    public StationApi(string baseUrl, int stationId, string stationToken) : base(baseUrl)
    {
        StationId = stationId;
        Headers["X-Station-Id"] = stationId.ToString();
        Headers["X-Station-Token"] = stationToken;
    }

    public Task<StationClientStateDto> StateAsync() => GetAsync<StationClientStateDto>("/api/station/state");
    public Task<List<ProductDto>> ProductsAsync() => GetAsync<List<ProductDto>>("/api/station/products");
    public Task<List<CategoryDto>> CategoriesAsync() => GetAsync<List<CategoryDto>>("/api/station/categories");
    public Task<SaleDto> OrderAsync(StationOrderRequest request) => PostAsync<SaleDto>("/api/station/order", request);
    public Task<byte[]> WallpaperFileAsync(int id) => GetBytesAsync($"/api/station/wallpapers/{id}/file");
    public Task<byte[]> PromoVideoFileAsync(int id) => GetBytesAsync($"/api/station/promo-videos/{id}/file");

    /// <summary>Lista activa de NetFiltroWeb que el agente o la pantalla deben aplicar.</summary>
    public Task<WebFilterPlaylistDto> WebFilterAsync() => GetAsync<WebFilterPlaylistDto>("/api/station/webfilter");
}
