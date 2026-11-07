using System.Globalization;
using System.Text.RegularExpressions;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>
/// Consulta la tasa oficial USD/Bs publicada en el sitio del Banco Central de Venezuela
/// y la guarda como la tasa activa del día correspondiente.
/// </summary>
public sealed class BcvRateService
{
    private const string BcvUrl = "https://www.bcv.org.ve/";

    private static readonly Regex UsdRegex = new(
        @"<div[^>]*\bid\s*=\s*[""']dolar[""'][^>]*>.*?<strong[^>]*\bclass\s*=\s*[""'][^""']*strong-tb[^""']*[""'][^>]*>\s*(?<value>[0-9.,\s]+)\s*</strong>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex ValueDateRegex = new(
        @"Fecha\s*Valor\s*:.*?content\s*=\s*[""'](?<date>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly Data.AppDbContext _db;
    private readonly ExchangeRateService _rates;
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<BcvRateService> _log;

    public BcvRateService(Data.AppDbContext db, ExchangeRateService rates,
        IHttpClientFactory factory, ILogger<BcvRateService> log)
    {
        _db = db;
        _rates = rates;
        _factory = factory;
        _log = log;
    }

    /// <summary>Consulta el BCV y registra la tasa (o informa si no cambió).</summary>
    public async Task<BcvRateDto> UpdateAsync(int? userId, CancellationToken ct = default)
    {
        var (rate, valueDate) = await FetchAsync(ct);

        // La tasa oficial se aplica al día en que se consulta; la fecha valor del BCV
        // queda registrada en las notas para fines de auditoría.
        var storedDate = ExchangeRateService.LocalDate();

        var existing = await _db.ExchangeRates
            .FirstOrDefaultAsync(x => x.Date == storedDate && x.Source == RateSource.Bcv, ct);

        if (existing is not null && existing.IsActive && existing.Rate == rate)
            return ToDto(existing, valueDate, false);

        var notes = $"Tasa oficial del BCV · fecha valor {valueDate:dd/MM/yyyy} · " +
                    $"consultada {DateTime.Now:dd/MM/yyyy HH:mm}";
        var saved = await _rates.SetAsync(
            new SetExchangeRateRequest(storedDate, rate, RateSource.Bcv, notes), userId);
        return ToDto(saved, valueDate, true);
    }

    /// <summary>Actualiza solo si la tasa publicada cambió; usado por el proceso automático.</summary>
    public async Task<BcvRateDto> UpdateIfChangedAsync(
        int? userId, CancellationToken ct = default)
    {
        var (rate, valueDate) = await FetchAsync(ct);
        var storedDate = ExchangeRateService.LocalDate();

        var existing = await _db.ExchangeRates
            .FirstOrDefaultAsync(x => x.Date == storedDate && x.Source == RateSource.Bcv, ct);

        if (existing is not null && existing.IsActive && existing.Rate == rate)
            return ToDto(existing, valueDate, false);

        var notes = $"Tasa oficial del BCV · fecha valor {valueDate:dd/MM/yyyy} · " +
                    $"consultada {DateTime.Now:dd/MM/yyyy HH:mm}";
        var saved = await _rates.SetAsync(
            new SetExchangeRateRequest(storedDate, rate, RateSource.Bcv, notes), userId);
        return ToDto(saved, valueDate, true);
    }

    private async Task<(decimal Rate, DateTime ValueDate)> FetchAsync(CancellationToken ct)
    {
        string html;
        try
        {
            var client = _factory.CreateClient("rates");
            html = await client.GetStringAsync(BcvUrl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "No se pudo descargar la página del BCV.");
            throw new AppException("No se pudo conectar con el sitio del BCV. " +
                                   "Verifique la conexión a Internet e intente de nuevo.");
        }

        var match = UsdRegex.Match(html);
        if (!match.Success || !TryParseRate(match.Groups["value"].Value, out var rate))
            throw new AppException("No se encontró la tasa del dólar en la página del BCV. " +
                                   "El formato del sitio pudo haber cambiado; registre la tasa manualmente.");

        if (rate <= 0 || rate >= 1_000_000_000m)
            throw new AppException("La tasa obtenida del BCV no es válida.");

        var dateMatch = ValueDateRegex.Match(html);
        var valueDate = ParseValueDate(dateMatch.Success ? dateMatch.Groups["date"].Value : null);
        return (rate, valueDate);
    }

    private static bool TryParseRate(string raw, out decimal rate)
    {
        raw = raw.Trim().Replace("\u00A0", string.Empty).Replace(" ", string.Empty);

        var venezuelan = CultureInfo.GetCultureInfo("es-VE");
        if (decimal.TryParse(raw, NumberStyles.Number, venezuelan, out rate) && rate > 0)
            return true;

        var normalized = raw.Replace(".", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out rate)
               && rate > 0;
    }

    private static DateTime ParseValueDate(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var withOffset))
            {
                var date = withOffset.Date.Date;
                if (date >= DateTime.Today.AddDays(-7) && date <= DateTime.Today.AddDays(7))
                    return date;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var plain))
            {
                var date = plain.Date;
                if (date >= DateTime.Today.AddDays(-7) && date <= DateTime.Today.AddDays(7))
                    return date;
            }
        }

        return ExchangeRateService.LocalDate();
    }

    private static BcvRateDto ToDto(ExchangeRate x, DateTime valueDate, bool updated)
        => new(x.Id, x.Date, x.Rate, x.Source, x.IsActive, x.CreatedUtc, x.Notes, valueDate, updated);

    private static BcvRateDto ToDto(ExchangeRateDto x, DateTime valueDate, bool updated)
        => new(x.Id, x.Date, x.Rate, x.Source, x.IsActive, x.CreatedUtc, x.Notes, valueDate, updated);
}

/// <summary>
/// Consulta periódicamente la tasa del BCV para mantenerla actualizada sin intervención.
/// </summary>
public sealed class BcvRateWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BcvRateWorker> _log;

    public BcvRateWorker(IServiceScopeFactory scopes, ILogger<BcvRateWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
                if (!await settings.GetBoolAsync(SettingsKeys.BcvAutoUpdate, true))
                    continue;

                var bcv = scope.ServiceProvider.GetRequiredService<BcvRateService>();
                var result = await bcv.UpdateIfChangedAsync(null, stoppingToken);
                if (result.Updated)
                    _log.LogInformation("Tasa BCV actualizada: 1 USD = {Rate} Bs (fecha valor {ValueDate:dd/MM/yyyy}).",
                        result.Rate, result.ValueDate);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo actualizar automáticamente la tasa del BCV.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
