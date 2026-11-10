using System.IO;
using System.Text.Json;
using CyberManager.Desktop.Api;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Desktop.Api;

/// <summary>Acceso tipado a la API del servidor.</summary>
public class CyberApi : ApiClient
{
    public CyberApi(string baseUrl) : base(baseUrl) { }

    // ---- auth ----
    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var result = await PostAsync<LoginResponse>("/api/auth/login", new LoginRequest(username, password));
        Token = result.Token;
        return result;
    }

    public Task LogoutAsync()
    {
        Token = null;
        return Task.CompletedTask;
    }

    public Task<string> GetCurrentUsernameAsync()
        => GetAsync<string>("/api/auth/me");

    // ---- tablero / equipos ----
    public Task<DashboardDto> DashboardAsync() => GetAsync<DashboardDto>("/api/dashboard");
    public Task<List<StationDto>> StationsAsync() => GetAsync<List<StationDto>>("/api/stations");
    public Task<List<SessionDto>> ActiveSessionsAsync() => GetAsync<List<SessionDto>>("/api/sessions/active");
    public Task<BootstrapDto> BootstrapAsync() => GetAsync<BootstrapDto>("/api/catalog");

    public Task<SessionDto> StartSessionAsync(StartSessionRequest req) => PostAsync<SessionDto>("/api/sessions/start", req);
    public Task<SessionDto> StopSessionAsync(int id, StopSessionRequest req) => PostAsync<SessionDto>($"/api/sessions/{id}/stop", req);
    public Task<SessionDto> ExtendSessionAsync(int id, ExtendSessionRequest req) => PostAsync<SessionDto>($"/api/sessions/{id}/extend", req);
    public Task<SessionDto> PauseSessionAsync(int id, bool pause) => PostAsync<SessionDto>($"/api/sessions/{id}/pause", new PauseSessionRequest(id, pause));
    public Task<SessionDto> TransferSessionAsync(int id, int target) => PostAsync<SessionDto>($"/api/sessions/{id}/transfer", new TransferSessionRequest(id, target));

    public Task CommandAsync(int stationId, AgentCommandType command, string? argument = null)
        => PostAsync($"/api/stations/{stationId}/command", new StationCommandRequest(command, argument));

    public Task<StationDto> MaintenanceAsync(int stationId, bool enabled)
        => PostAsync<StationDto>($"/api/stations/{stationId}/maintenance?enabled={enabled.ToString().ToLower()}", new { });

    /// <summary>Envía un paquete Wake on LAN al equipo (requiere MAC configurada).</summary>
    public Task WakeStationAsync(int stationId)
        => PostAsync($"/api/stations/{stationId}/wake", new { });

    public Task<StationDto> SaveStationAsync(int? id, SaveStationRequest req)
        => id is int sid ? PutAsync<StationDto>($"/api/stations/{sid}", req) : PostAsync<StationDto>("/api/stations", req);

    public Task DeleteStationAsync(int id) => DeleteAsync($"/api/stations/{id}");

    public Task<bool> StationHasActiveSessionsAsync(int id)
        => GetAsync<bool>($"/api/stations/{id}/has-active-sessions");

    public Task<StationDto> DeactivateStationAsync(int id)
        => PutAsync<StationDto>($"/api/stations/{id}/deactivate", new { Active = false });

    public Task<StationConfigDto> StationConfigAsync(int id) => GetAsync<StationConfigDto>($"/api/stations/{id}/config");

    public Task<object> RegenerateTokenAsync(int id) => PostAsync<object>($"/api/stations/{id}/token", new { });
    public Task ReorderStationsAsync(List<int> stationIds)
        => PostAsync("/api/stations/reorder", new ReorderStationsRequest(stationIds));

    // ---- catálogo ----
    public Task<List<ProductDto>> ProductsAsync(bool includeInactive = false)
        => GetAsync<List<ProductDto>>($"/api/products?includeInactive={includeInactive.ToString().ToLower()}");

    public Task<ProductDto> SaveProductAsync(int? id, SaveProductRequest req)
        => id is int pid ? PutAsync<ProductDto>($"/api/products/{pid}", req) : PostAsync<ProductDto>("/api/products", req);

    public Task DeleteProductAsync(int id) => DeleteAsync($"/api/products/{id}");

    public Task<List<CategoryDto>> CategoriesAsync() => GetAsync<List<CategoryDto>>("/api/categories");
    public Task<CategoryDto> SaveCategoryAsync(int? id, SaveCategoryRequest req)
        => id is int cid ? PutAsync<CategoryDto>($"/api/categories/{cid}", req) : PostAsync<CategoryDto>("/api/categories", req);

    public Task DeleteCategoryAsync(int id) => DeleteAsync($"/api/categories/{id}");

    public Task<List<RateDto>> RatesAsync() => GetAsync<List<RateDto>>("/api/rates");
    public Task<RateDto> SaveRateAsync(int? id, SaveRateRequest req)
        => id is int rid ? PutAsync<RateDto>($"/api/rates/{rid}", req) : PostAsync<RateDto>("/api/rates", req);

    public Task DeleteRateAsync(int id) => DeleteAsync($"/api/rates/{id}");

    public Task<List<ZoneDto>> ZonesAsync() => GetAsync<List<ZoneDto>>("/api/zones");
    public Task<ZoneDto> SaveZoneAsync(int? id, SaveZoneRequest req)
        => id is int zid ? PutAsync<ZoneDto>($"/api/zones/{zid}", req) : PostAsync<ZoneDto>("/api/zones", req);

    public Task DeleteZoneAsync(int id) => DeleteAsync($"/api/zones/{id}");

    public Task<List<TimePackageDto>> PackagesAsync() => GetAsync<List<TimePackageDto>>("/api/time-packages");
    public Task<TimePackageDto> SavePackageAsync(int? id, SaveTimePackageRequest req)
        => id is int pid ? PutAsync<TimePackageDto>($"/api/time-packages/{pid}", req) : PostAsync<TimePackageDto>("/api/time-packages", req);

    public Task DeletePackageAsync(int id) => DeleteAsync($"/api/time-packages/{id}");

    // ---- ventas ----
    public Task<SaleDto> CreateSaleAsync(CreateSaleRequest req) => PostAsync<SaleDto>("/api/sales", req);
    public Task<List<SaleDto>> SalesAsync(DateTime from, DateTime to)
        => GetAsync<List<SaleDto>>($"/api/sales?from={from:O}&to={to:O}");
    public Task<SaleDto> CancelSaleAsync(long id) => PostAsync<SaleDto>($"/api/sales/{id}/cancel", new { });
    public Task<InvoiceDto> InvoiceAsync(long id) => GetAsync<InvoiceDto>($"/api/sales/{id}/invoice");

    // ---- caja ----
    public Task<CashSessionDto> CashCurrentAsync() => GetAsync<CashSessionDto>("/api/cash/current");
    public Task<CashSessionDto> CashOpenAsync(CashOpenRequest req) => PostAsync<CashSessionDto>("/api/cash/open", req);
    public Task<CashSessionDto> CashCloseAsync(CashCloseRequest req) => PostAsync<CashSessionDto>("/api/cash/close", req);
    public Task<CashSessionDto> CashMovementAsync(CashMovementRequest req) => PostAsync<CashSessionDto>("/api/cash/movements", req);
    public Task<List<CashReportRow>> CashHistoryAsync(DateTime from, DateTime to)
        => GetAsync<List<CashReportRow>>($"/api/reports/cash?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    // ---- inventario ----
    public Task<List<StockMovementDto>> StockMovementsAsync(int? productId = null)
        => GetAsync<List<StockMovementDto>>($"/api/stock/movements{(productId is int p ? $"?productId={p}" : string.Empty)}");

    public Task AddStockMovementAsync(StockMovementRequest req) => PostAsync("/api/stock/movements", req);
    public Task<List<SupplierDto>> SuppliersAsync() => GetAsync<List<SupplierDto>>("/api/suppliers");
    public Task<SupplierDto> SaveSupplierAsync(int? id, SaveSupplierRequest req)
        => id is int sid ? PutAsync<SupplierDto>($"/api/suppliers/{sid}", req) : PostAsync<SupplierDto>("/api/suppliers", req);
    public Task<List<PurchaseDto>> PurchasesAsync() => GetAsync<List<PurchaseDto>>("/api/purchases");
    public Task CreatePurchaseAsync(CreatePurchaseRequest req) => PostAsync("/api/purchases", req);

    // ---- pagos a proveedores ----
    public Task<List<SupplierBalanceDto>> SupplierBalancesAsync() => GetAsync<List<SupplierBalanceDto>>("/api/suppliers/balances");
    public Task<List<SupplierPaymentDto>> SupplierPaymentsAsync(int supplierId) => GetAsync<List<SupplierPaymentDto>>($"/api/suppliers/{supplierId}/payments");
    public Task<SupplierPaymentDto> CreateSupplierPaymentAsync(int supplierId, CreateSupplierPaymentRequest req) => PostAsync<SupplierPaymentDto>($"/api/suppliers/{supplierId}/payments", req);

    public Task<List<SupplierPaymentDto>> AllSupplierPaymentsAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = new List<string>();
        if (from is DateTime f) query.Add($"from={f:yyyy-MM-dd}");
        if (to is DateTime t) query.Add($"to={t:yyyy-MM-dd}");
        var suffix = query.Count > 0 ? $"?{string.Join('&', query)}" : string.Empty;
        return GetAsync<List<SupplierPaymentDto>>($"/api/supplier-payments{suffix}");
    }

    // ---- clientes ----
    public Task<List<CustomerDto>> CustomersAsync(string? q = null)
        => GetAsync<List<CustomerDto>>($"/api/customers{(string.IsNullOrWhiteSpace(q) ? string.Empty : $"?q={Uri.EscapeDataString(q)}")}");

    public Task<CustomerDto> SaveCustomerAsync(int? id, SaveCustomerRequest req)
        => id is int cid ? PutAsync<CustomerDto>($"/api/customers/{cid}", req) : PostAsync<CustomerDto>("/api/customers", req);

    public Task<CustomerDto> RechargeAsync(int id, RechargeRequest req) => PostAsync<CustomerDto>($"/api/customers/{id}/recharge", req);
    public Task<List<CustomerTransactionDto>> CustomerTransactionsAsync(int id) => GetAsync<List<CustomerTransactionDto>>($"/api/customers/{id}/transactions");

    // ---- reservas ----
    public Task<List<ReservationDto>> ReservationsAsync() => GetAsync<List<ReservationDto>>("/api/reservations");
    public Task SaveReservationAsync(int? id, SaveReservationRequest req)
    {
        if (id is int rid) return PutAsync($"/api/reservations/{rid}", req);
        return PostAsync("/api/reservations", req);
    }
    public Task CancelReservationAsync(int id) => DeleteAsync($"/api/reservations/{id}");

    // ---- tasas ----
    public Task<ExchangeRateDto> TodayRateAsync() => GetAsync<ExchangeRateDto>("/api/exchange-rates/today");
    public Task<ExchangeRateDto> SetRateAsync(SetExchangeRateRequest req) => PostAsync<ExchangeRateDto>("/api/exchange-rates", req);
    public Task<BcvRateDto> FetchBcvRateAsync() => PostAsync<BcvRateDto>("/api/exchange-rates/bcv", new { });
    public Task<List<ExchangeRateDto>> RateHistoryAsync() => GetAsync<List<ExchangeRateDto>>("/api/exchange-rates");

    // ---- reportes ----
    public Task<ReportSummaryDto> ReportSummaryAsync(DateTime from, DateTime to)
        => GetAsync<ReportSummaryDto>($"/api/reports/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
    public Task<List<SalesReportRow>> ReportSalesAsync(DateTime from, DateTime to)
        => GetAsync<List<SalesReportRow>>($"/api/reports/sales?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
    public Task<List<ProductReportRow>> ReportProductsAsync(DateTime from, DateTime to)
        => GetAsync<List<ProductReportRow>>($"/api/reports/products?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
    public Task<List<StationReportRow>> ReportStationsAsync(DateTime from, DateTime to)
        => GetAsync<List<StationReportRow>>($"/api/reports/stations?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
    public Task<List<InventoryReportRow>> ReportInventoryAsync() => GetAsync<List<InventoryReportRow>>("/api/reports/inventory");
    public Task<DailyClosingDto> DailyClosingAsync(DateTime date)
        => GetAsync<DailyClosingDto>($"/api/reports/daily-closing?date={date:yyyy-MM-dd}");

    public Task<DailyReportEmailResultDto> SendDailyClosingEmailAsync(DateTime date, bool force = false)
        => PostAsync<DailyReportEmailResultDto>(
            $"/api/reports/daily-closing/email?date={date:yyyy-MM-dd}&force={(force ? "true" : "false")}",
            new DailyReportEmailRequest(date, force));

    public Task<DailyReportEmailResultDto> SendTestEmailAsync(TestEmailRequest? request = null)
        => PostAsync<DailyReportEmailResultDto>("/api/reports/daily-closing/email/test",
            request ?? new TestEmailRequest());

    // ---- galería de fondos ----
    public Task<List<WallpaperDto>> WallpapersAsync() => GetAsync<List<WallpaperDto>>("/api/wallpapers");
    public Task<WallpaperPlaylistDto> WallpaperPlaylistAsync() => GetAsync<WallpaperPlaylistDto>("/api/wallpapers/playlist");
    public Task<byte[]> WallpaperFileAsync(int id) => GetBytesAsync($"/api/wallpapers/{id}/file");

    public Task<WallpaperDto> UploadWallpaperAsync(string fileName, byte[] data, string contentType,
        string? name = null, int? sortOrder = null)
    {
        var fields = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(name))
            fields.Add(new KeyValuePair<string, string>("name", name));
        if (sortOrder is int order)
            fields.Add(new KeyValuePair<string, string>("sortOrder", order.ToString()));

        return PostFileAsync<WallpaperDto>("/api/wallpapers", "file", fileName, contentType, data, fields);
    }

    public Task<WallpaperDto> UpdateWallpaperAsync(int id, SaveWallpaperRequest req) => PutAsync<WallpaperDto>($"/api/wallpapers/{id}", req);

    public Task DeleteWallpaperAsync(int id) => DeleteAsync($"/api/wallpapers/{id}");

    public Task<WallpaperPlaylistDto> ApplyWallpaperAsync(ApplyWallpaperSettingsRequest req) => PostAsync<WallpaperPlaylistDto>("/api/wallpapers/apply", req);

    public Task<WallpaperPlaylistDto> RefreshWallpapersAsync() => PostAsync<WallpaperPlaylistDto>("/api/wallpapers/refresh", new { });

    // ---- videos promocionales ----
    public Task<List<PromoVideoDto>> PromoVideosAsync() => GetAsync<List<PromoVideoDto>>("/api/promo-videos");
    public Task<PromoVideoPlaylistDto> PromoVideoPlaylistAsync() => GetAsync<PromoVideoPlaylistDto>("/api/promo-videos/playlist");

    public Task<PromoVideoDto> UploadPromoVideoAsync(string fileName, byte[] data, string contentType,
        string? name = null, int? sortOrder = null)
    {
        var fields = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(name))
            fields.Add(new KeyValuePair<string, string>("name", name));
        if (sortOrder is int order)
            fields.Add(new KeyValuePair<string, string>("sortOrder", order.ToString()));

        return PostFileAsync<PromoVideoDto>("/api/promo-videos", "file", fileName, contentType, data, fields);
    }

    public Task<PromoVideoDto> UpdatePromoVideoAsync(int id, SavePromoVideoRequest req) => PutAsync<PromoVideoDto>($"/api/promo-videos/{id}", req);

    public Task DeletePromoVideoAsync(int id) => DeleteAsync($"/api/promo-videos/{id}");

    public Task<PromoVideoPlaylistDto> RefreshPromoVideosAsync() => PostAsync<PromoVideoPlaylistDto>("/api/promo-videos/refresh", new { });

    // ---- NetFiltroWeb ----
    public Task<List<WebFilterRuleDto>> WebFilterRulesAsync() => GetAsync<List<WebFilterRuleDto>>("/api/webfilter");

    public Task<WebFilterRuleDto> SaveWebFilterRuleAsync(int? id, SaveWebFilterRuleRequest req)
        => id is int rid
            ? PutAsync<WebFilterRuleDto>($"/api/webfilter/{rid}", req)
            : PostAsync<WebFilterRuleDto>("/api/webfilter", req);

    public Task DeleteWebFilterRuleAsync(int id) => DeleteAsync($"/api/webfilter/{id}");

    public Task<object> RestoreWebFilterDefaultsAsync() => PostAsync<object>("/api/webfilter/restore", new { });

    // ---- respaldos de la base de datos ----
    public Task<BackupListDto> BackupsAsync() => GetAsync<BackupListDto>("/api/backups");

    public Task<BackupFileDto> CreateBackupAsync() => PostAsync<BackupFileDto>("/api/backups", new { });

    public Task<byte[]> DownloadBackupAsync(string fileName) => GetBytesAsync($"/api/backups/{Uri.EscapeDataString(fileName)}/download");

    public Task<RestoreBackupResultDto> RestoreStoredBackupAsync(string fileName) => PostAsync<RestoreBackupResultDto>(
        $"/api/backups/{Uri.EscapeDataString(fileName)}/restore", new { });

    public Task<RestoreBackupResultDto> RestoreBackupUploadAsync(string fileName, byte[] data) => PostFileAsync<RestoreBackupResultDto>("/api/backups/restore-upload", "file",
        fileName, "application/octet-stream", data);

    // ---- configuración / usuarios ----
    public Task<Dictionary<string, string>> SettingsAsync() => GetAsync<Dictionary<string, string>>("/api/settings");
    public Task<Dictionary<string, string>> SaveSettingsAsync(Dictionary<string, string> values) => PutAsync<Dictionary<string, string>>("/api/settings", values);
    public Task<List<UserDto>> UsersAsync() => GetAsync<List<UserDto>>("/api/users");
    public Task<UserDto> CreateUserAsync(CreateUserRequest req) => PostAsync<UserDto>("/api/users", req);
    public Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest req) => PutAsync<UserDto>($"/api/users/{id}", req);
    public Task DeactivateUserAsync(int id) => DeleteAsync($"/api/users/{id}");
    public Task DeleteUserPermanentlyAsync(int id) => DeleteAsync($"/api/users/{id}/permanent");

    // ---- actualizaciones publicadas en GitHub ----
    public Task<UpdateInfoDto> LatestUpdateAsync(bool refresh = false, bool cached = false)
        => GetAsync<UpdateInfoDto>(
            $"/api/updates/latest?refresh={(refresh ? "true" : "false")}&cached={(cached ? "true" : "false")}");

    public Task<UpdateInfoDto> DismissUpdateAsync(string version) => PostAsync<UpdateInfoDto>("/api/updates/dismiss", new DismissUpdateRequest(version));
}

public static class AppPaths
{
    public static string DataDir(string app)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CyberManager", app);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ConfigFile(string app) => Path.Combine(DataDir(app), "config.json");
}

public class AppConfig
{
    public string ServerUrl { get; set; } = "http://127.0.0.1:5080";
    public string? LastUsername { get; set; }
    public int StationId { get; set; }
    public string StationToken { get; set; } = string.Empty;
    public string? StationIpAddress { get; set; }
    public string? StationMacAddress { get; set; }
    /// <summary>Idioma de la estación: "es" o "en".</summary>
    public string Language { get; set; } = "es";

    public static AppConfig Load(string app)
    {
        try
        {
            var file = AppPaths.ConfigFile(app);
            if (!File.Exists(file)) return new AppConfig();
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(file)) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(string app) => File.WriteAllText(AppPaths.ConfigFile(app), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
}