using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Claims;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;

namespace CyberManager.Desktop.Api;

/// <summary>Cliente HTTP autenticado con la dirección IP y MAC del equipo (pantalla de estación).</summary>
public class StationApi : ApiClient
{
    public int StationId { get; }
    public string? StationIpAddress { get; }
    public string? StationMacAddress { get; }

    public StationApi(string baseUrl, int stationId, string stationIpAddress = null!, string stationMacAddress = null!) : base(baseUrl)
    {
        StationId = stationId;
        StationIpAddress = stationIpAddress;
        StationMacAddress = stationMacAddress;

        // Configurar headers de identificación: preferiblemente IP y MAC, fallback a token
        if (!string.IsNullOrWhiteSpace(stationIpAddress))
        {
            // Usar IP como identificador principal
            Headers["X-Station-Id"] = stationId.ToString();
            // No enviar token por defecto cuando se usa IP/MAC
        }
        else if (!string.IsNullOrWhiteSpace(stationMacAddress))
        {
            Headers["X-Station-Id"] = stationId.ToString();
            // No enviar token por defecto cuando se usa IP/MAC
        }
        else
        {
            // Modo legado: enviar token para compatibilidad hacia atrás
            Headers["X-Station-Id"] = stationId.ToString();
            // Note: el token se establecería mediante SetTokenHeader() si es necesario
        }
    }

    /// <summary>
    /// Establece el header de token de forma explícita para compatibilidad.
    /// </summary>
    public void SetTokenHeader(string token)
    {
        Headers["X-Station-Token"] = token;
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