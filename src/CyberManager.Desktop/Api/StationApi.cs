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

    /// <summary>Autenticación por token del equipo (método principal).</summary>
    public StationApi(string baseUrl, int stationId, string? token = null, TimeSpan? timeout = null)
        : base(baseUrl, timeout)
    {
        StationId = stationId;
        Headers["X-Station-Id"] = stationId.ToString();
        if (!string.IsNullOrWhiteSpace(token))
            Headers["X-Station-Token"] = token;
    }

    /// <summary>Autenticación por IP y MAC (compatibilidad) con token opcional.</summary>
    public StationApi(string baseUrl, int stationId, string ipAddress, string macAddress,
        string? token = null, TimeSpan? timeout = null) : base(baseUrl, timeout)
    {
        StationId = stationId;
        StationIpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress;
        StationMacAddress = string.IsNullOrWhiteSpace(macAddress) ? null : macAddress;

        Headers["X-Station-Id"] = stationId.ToString();
        if (StationIpAddress is not null) Headers["X-Station-Ip"] = StationIpAddress;
        if (StationMacAddress is not null) Headers["X-Station-Mac"] = StationMacAddress;
        if (!string.IsNullOrWhiteSpace(token)) Headers["X-Station-Token"] = token;
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

    /// <summary>Descarga el video a disco en bloques: evita timeouts y picos de RAM en el terminal.</summary>
    public Task DownloadPromoVideoAsync(int id, string destination)
        => DownloadToFileAsync($"/api/station/promo-videos/{id}/file", destination);

    /// <summary>Lista activa de NetFiltroWeb que el agente o la pantalla deben aplicar.</summary>
    public Task<WebFilterPlaylistDto> WebFilterAsync() => GetAsync<WebFilterPlaylistDto>("/api/station/webfilter");

    // ---- Autoservicio prepago ----
    public Task<PrepaidInfoDto> PrepaidInfoAsync() => GetAsync<PrepaidInfoDto>("/api/station/prepaid/info");
    public Task<PrepaidAccountDto> PrepaidRegisterAsync(PrepaidRegisterRequest request)
        => PostAsync<PrepaidAccountDto>("/api/station/prepaid/register", request);
    public Task<PrepaidLoginResponse> PrepaidLoginAsync(PrepaidLoginRequest request)
        => PostAsync<PrepaidLoginResponse>("/api/station/prepaid/login", request);
    public Task<PrepaidRechargeResultDto> PrepaidRechargeAsync(PrepaidRechargeRequest request)
        => PostAsync<PrepaidRechargeResultDto>("/api/station/prepaid/recharge", request);
    public Task<PrepaidStartResultDto> PrepaidStartAsync(PrepaidStartRequest request)
        => PostAsync<PrepaidStartResultDto>("/api/station/prepaid/start", request);
    public Task<PrepaidFinishResultDto> PrepaidFinishAsync()
        => PostAsync<PrepaidFinishResultDto>("/api/station/prepaid/finish", new { });
}