using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public static class SettingsKeys
{
    public const string BusinessName = "BusinessName";
    public const string BusinessTaxId = "BusinessTaxId";
    public const string BusinessAddress = "BusinessAddress";
    public const string BusinessPhone = "BusinessPhone";
    public const string CurrencySymbol = "CurrencySymbol";
    public const string LocalCurrencySymbol = "LocalCurrencySymbol";
    public const string DefaultTaxRate = "DefaultTaxRate";
    public const string IgtfEnabled = "IgtfEnabled";
    public const string IgtfRate = "IgtfRate";
    public const string TicketHeader = "TicketHeader";
    public const string TicketFooter = "TicketFooter";
    public const string AutoPrintTicket = "AutoPrintTicket";
    public const string TicketCopies = "TicketCopies";
    public const string DailyClosingFormat = "DailyClosingFormat";
    public const string ScannerAutoAdd = "ScannerAutoAdd";
    public const string WarningMinutes = "WarningMinutes";
    public const string GraceMinutes = "GraceMinutes";
    public const string AutoLockOvertime = "AutoLockOvertime";
    public const string AllowPostpaid = "AllowPostpaid";
    public const string PrinterName = "PrinterName";
    public const string KioskMode = "KioskMode";
    public const string InvoicePrefix = "InvoicePrefix";
    public const string ControlNumberStart = "ControlNumberStart";
    public const string BackupAutoEnabled = "BackupAutoEnabled";
    public const string BackupIntervalHours = "BackupIntervalHours";
    public const string BackupRetention = "BackupRetention";
    public const string BcvAutoUpdate = "BcvAutoUpdate";
    public const string UpdateReminderEnabled = "UpdateReminderEnabled";
    public const string UpdateReminderDelayHours = "UpdateReminderDelayHours";
    public const string UpdateReminderDismissedVersion = "UpdateReminderDismissedVersion";
    public const string DailyReportEmailEnabled = "DailyReportEmailEnabled";
    public const string DailyReportEmailRecipients = "DailyReportEmailRecipients";
    public const string DailyReportEmailLastSentDate = "DailyReportEmailLastSentDate";
    public const string SmtpHost = "SmtpHost";
    public const string SmtpPort = "SmtpPort";
    public const string SmtpUseSsl = "SmtpUseSsl";
    public const string SmtpUsername = "SmtpUsername";
    public const string SmtpPassword = "SmtpPassword";
    public const string SmtpFromAddress = "SmtpFromAddress";
    public const string SmtpFromName = "SmtpFromName";
    public const string WallpaperMode = WallpaperSettings.Mode;
    public const string WallpaperRotationSeconds = WallpaperSettings.RotationSeconds;
    public const string WallpaperActiveId = WallpaperSettings.ActiveId;
    /// <summary>Versión del paquete de fondos incluido con el instalador.</summary>
    public const string BundledWallpapersVersion = "BundledWallpapersVersion";
    /// <summary>Texto descriptivo que muestra la barra flotante del cliente.</summary>
    public const string SessionBarDescription = "SessionBarDescription";
    /// <summary>Escala de la barra flotante del cliente en porcentaje (70 a 160).</summary>
    public const string SessionBarScale = "SessionBarScale";
    /// <summary>Ruta del visor VNC externo (opcional).</summary>
    public const string VncViewerPath = "VncViewerPath";
    /// <summary>Puerto VNC por defecto para los equipos.</summary>
    public const string VncDefaultPort = "VncDefaultPort";
    /// <summary>Versión de la lista inicial de NetFiltroWeb.</summary>
    public const string WebFilterSeedVersion = "WebFilterSeedVersion";
    /// <summary>Activa el proxy local del agente para aplicar reglas por palabra clave.</summary>
    public const string WebFilterProxyEnabled = "WebFilterProxyEnabled";
    /// <summary>Puerto del proxy local de NetFiltroWeb.</summary>
    public const string WebFilterProxyPort = "WebFilterProxyPort";
}

public class SettingsService
{
    private readonly Data.AppDbContext _db;
    public SettingsService(Data.AppDbContext db) => _db = db;

    public async Task<string> GetAsync(string key, string fallback = "")
    {
        var s = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
        return s?.Value ?? fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback = 0)
        => decimal.TryParse(await GetAsync(key), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public async Task<int> GetIntAsync(string key, int fallback = 0)
        => int.TryParse(await GetAsync(key), out var v) ? v : fallback;

    public async Task<bool> GetBoolAsync(string key, bool fallback = false)
        => bool.TryParse(await GetAsync(key), out var v) ? v : fallback;

    public async Task<Dictionary<string, string>> GetAllAsync()
        => await _db.Settings.AsNoTracking()
            // La contraseña SMTP nunca se expone a las consolas ni a las estaciones.
            .Where(x => x.Key != SettingsKeys.SmtpPassword)
            .ToDictionaryAsync(x => x.Key, x => x.Value);

    public async Task SetAsync(string key, string value, string? description = null)
    {
        var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
        if (s is null)
        {
            s = new Setting { Key = key, Value = value, Description = description };
            _db.Settings.Add(s);
        }
        else
        {
            s.Value = value;
            if (description is not null) s.Description = description;
        }
        await _db.SaveChangesAsync();
    }

    public async Task SetManyAsync(IEnumerable<KeyValuePair<string, string>> values)
    {
        foreach (var kv in values) await SetAsync(kv.Key, kv.Value);
    }
}

public static class Money
{
    public static decimal ToVes(decimal usd, decimal rate)
        => Math.Round(usd * rate, 2, MidpointRounding.AwayFromZero);
}

public class ExchangeRateService
{
    private readonly Data.AppDbContext _db;
    public ExchangeRateService(Data.AppDbContext db) => _db = db;

    public static DateTime LocalDate(DateTime? utc = null)
        => (utc ?? DateTime.UtcNow).ToLocalTime().Date;

    /// <summary>Tasa activa del día. Si no hay del día, devuelve la última registrada.</summary>
    public async Task<ExchangeRate?> GetActiveAsync(DateTime? utc = null)
    {
        var date = LocalDate(utc);
        var today = await _db.ExchangeRates.AsNoTracking()
            .Where(x => x.IsActive && x.Date == date)
            .OrderByDescending(x => x.Source == RateSource.Bcv)
            .ThenByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();

        return today ?? await _db.ExchangeRates.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<decimal> GetRateAsync(DateTime? utc = null)
        => (await GetActiveAsync(utc))?.Rate ?? 0m;

    public async Task<ExchangeRateDto> SetAsync(SetExchangeRateRequest req, int? userId)
    {
        if (req.Rate <= 0) throw new AppException("La tasa debe ser mayor que cero.");
        var date = req.Date.Date;
        if (date > DateTime.Today.AddDays(1))
            throw new AppException("No se puede registrar una tasa con fecha futura.");

        var others = await _db.ExchangeRates
            .Where(x => x.Date == date && x.IsActive && x.Source != req.Source)
            .ToListAsync();
        foreach (var o in others) o.IsActive = false;

        var rate = await _db.ExchangeRates
            .FirstOrDefaultAsync(x => x.Date == date && x.Source == req.Source);

        if (rate is null)
        {
            rate = new ExchangeRate { Date = date, Source = req.Source };
            _db.ExchangeRates.Add(rate);
        }

        rate.Rate = req.Rate;
        rate.IsActive = true;
        rate.Notes = req.Notes;
        rate.CreatedByUserId = userId;
        rate.CreatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new ExchangeRateDto(rate.Id, rate.Date, rate.Rate, rate.Source, rate.IsActive, rate.CreatedUtc, rate.Notes);
    }

    public async Task<List<ExchangeRateDto>> HistoryAsync(DateTime from, DateTime to)
        => await _db.ExchangeRates.AsNoTracking()
            .Where(x => x.Date >= from.Date && x.Date <= to.Date)
            .OrderByDescending(x => x.Date)
            .Select(x => new ExchangeRateDto(x.Id, x.Date, x.Rate, x.Source, x.IsActive, x.CreatedUtc, x.Notes))
            .ToListAsync();
}
