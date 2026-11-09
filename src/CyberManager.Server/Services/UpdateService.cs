using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Services;

/// <summary>Una versión publicada en GitHub, ya normalizada.</summary>
public sealed record GithubReleaseInfo(
    string Version,
    string TagName,
    string Name,
    string Url,
    string? Notes,
    DateTime? PublishedUtc);

/// <summary>Foto del estado de la última consulta a GitHub.</summary>
public sealed record UpdateSnapshot(
    GithubReleaseInfo? Release,
    DateTime CheckedUtc,
    bool Failed,
    string? Error);

/// <summary>
/// Consulta las versiones publicadas en GitHub y guarda el resultado en memoria para no
/// agotar el límite de peticiones de la API (60 por hora sin autenticación).
/// </summary>
public sealed class UpdateService
{
    public const string Owner = "Kis4r4gi";
    public const string Repository = "CyberManager";

    private static readonly Uri ReleasesApi =
        new($"https://api.github.com/repos/{Owner}/{Repository}/releases?per_page=20");

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _http;
    private readonly ILogger<UpdateService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GithubReleaseInfo? _release;
    private DateTime _checkedUtc = DateTime.MinValue;
    private bool _failed;
    private string? _error;

    public UpdateService(IHttpClientFactory http, ILogger<UpdateService> log)
    {
        _http = http;
        _log = log;
    }

    /// <summary>Devuelve el último resultado guardado en memoria sin consultar GitHub.</summary>
    public UpdateSnapshot GetCachedSnapshot() => Snapshot();

    /// <summary>Devuelve la versión más reciente publicada; usa la caché salvo que se fuerce.</summary>
    public async Task<UpdateSnapshot> GetSnapshotAsync(bool force, CancellationToken ct = default)
    {
        if (!force && IsFresh) return Snapshot();

        await _gate.WaitAsync(ct);
        try
        {
            if (!force && IsFresh) return Snapshot();

            try
            {
                using var client = _http.CreateClient("github");
                using var response = await client.GetAsync(ReleasesApi, ct);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _release = null;
                    _failed = false;
                    _error = null;
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    var items = await response.Content.ReadFromJsonAsync<List<GithubRelease>>(cancellationToken: ct)
                                ?? new List<GithubRelease>();

                    _release = items
                        .Where(r => r is { Draft: false, Prerelease: false })
                        .Select(ToInfo)
                        .Where(r => r is not null)
                        .Select(r => r!)
                        .OrderByDescending(r => ParseVersion(r.Version))
                        .FirstOrDefault();

                    _failed = false;
                    _error = null;
                }

                _checkedUtc = DateTime.UtcNow;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _failed = true;
                _error = ex.Message;
                _checkedUtc = DateTime.UtcNow;
                _log.LogWarning(ex, "No se pudo consultar la última versión publicada en GitHub.");
            }

            return Snapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh => DateTime.UtcNow - _checkedUtc < CacheDuration;

    private UpdateSnapshot Snapshot() => new(_release, _checkedUtc, _failed, _error);

    private static GithubReleaseInfo? ToInfo(GithubRelease release)
    {
        var version = ParseVersion(release.TagName);
        if (version is null) return null;

        DateTime? published = DateTime.TryParse(release.PublishedAt, out var date)
            ? date.ToUniversalTime()
            : null;

        return new GithubReleaseInfo(
            version.ToString(3),
            release.TagName ?? version.ToString(3),
            string.IsNullOrWhiteSpace(release.Name) ? release.TagName ?? version.ToString(3) : release.Name!,
            release.HtmlUrl ?? $"https://github.com/{Owner}/{Repository}/releases",
            Truncate(release.Body, 4000),
            published);
    }

    /// <summary>Convierte etiquetas como <c>v1.2.3</c> o <c>1.2.3-beta</c> en una versión comparable.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var text = tag.Trim().TrimStart('v', 'V');
        var suffix = text.IndexOfAny(new[] { '-', '+' });
        if (suffix >= 0) text = text[..suffix];

        return Version.TryParse(text, out var version) ? version : null;
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    private sealed class GithubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("published_at")] public string? PublishedAt { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
    }
}

/// <summary>Combina la consulta a GitHub con la configuración del negocio.</summary>
public sealed class UpdateReminderService
{
    private readonly SettingsService _settings;
    private readonly UpdateService _updates;

    public UpdateReminderService(SettingsService settings, UpdateService updates)
    {
        _settings = settings;
        _updates = updates;
    }

    public async Task<UpdateInfoDto> GetStatusAsync(bool force = false, CancellationToken ct = default)
    {
        var snapshot = await _updates.GetSnapshotAsync(force, ct);
        return await BuildStatusAsync(snapshot);
    }

    /// <summary>Arma el estado con el resultado que ya está en memoria, sin consultar GitHub.</summary>
    public async Task<UpdateInfoDto> GetCachedStatusAsync()
    {
        var snapshot = _updates.GetCachedSnapshot();
        return await BuildStatusAsync(snapshot);
    }

    private async Task<UpdateInfoDto> BuildStatusAsync(UpdateSnapshot snapshot)
    {
        var currentVersion = AppInfo.Version;
        var current = UpdateService.ParseVersion(currentVersion);

        var enabled = await _settings.GetBoolAsync(SettingsKeys.UpdateReminderEnabled, true);
        var delayHours = Math.Clamp(
            await _settings.GetIntAsync(SettingsKeys.UpdateReminderDelayHours, 2), 0, 168);
        var dismissed = await _settings.GetAsync(SettingsKeys.UpdateReminderDismissedVersion);

        var release = snapshot.Release;
        var latest = release is null ? null : UpdateService.ParseVersion(release.Version);
        var isNewer = release is not null && current is not null && latest is not null &&
                      latest > current;

        var reminderAt = release?.PublishedUtc?.AddHours(delayHours);
        var isDismissed = release is not null &&
                          string.Equals(dismissed?.Trim(), release.Version, StringComparison.OrdinalIgnoreCase);
        var due = enabled && isNewer && !isDismissed &&
                  reminderAt is not null && DateTime.UtcNow >= reminderAt.Value;

        return new UpdateInfoDto(
            currentVersion,
            release?.Version,
            isNewer,
            enabled,
            delayHours,
            release?.PublishedUtc,
            reminderAt,
            due,
            isDismissed,
            release?.Url,
            release?.Name,
            release?.Notes,
            snapshot.CheckedUtc,
            snapshot.Failed,
            snapshot.Error);
    }

    /// <summary>Oculta el aviso de esta versión hasta que se publique una más reciente.</summary>
    public async Task DismissAsync(string version)
    {
        var normalized = UpdateService.ParseVersion(version)?.ToString(3) ?? version.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return;
        await _settings.SetAsync(SettingsKeys.UpdateReminderDismissedVersion, normalized);
    }
}
