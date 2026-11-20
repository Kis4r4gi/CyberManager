using CyberManager.Server.Data;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>
/// Construye la configuración de fondos que consulta cada estación. El servidor solo
/// publica la galería activa y los parámetros de reproducción; cada cliente decide
/// cuándo avanzar, en qué orden y con qué transición.
/// </summary>
public class WallpaperService
{
    private readonly AppDbContext _db;
    private readonly SettingsService _settings;

    public WallpaperService(AppDbContext db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<WallpaperPlaylistDto> PlaylistAsync()
    {
        var seconds = await _settings.GetIntAsync(
            WallpaperSettings.RotationSeconds, WallpaperSettings.DefaultRotationSeconds);
        seconds = Math.Clamp(seconds, WallpaperSettings.MinRotationSeconds,
            WallpaperSettings.MaxRotationSeconds);

        var order = WallpaperSettings.ParseOrder(
            await _settings.GetAsync(WallpaperSettings.Order, nameof(WallpaperOrder.Ordered)));

        var transition = WallpaperSettings.ParseTransition(
            await _settings.GetAsync(WallpaperSettings.Transition, nameof(WallpaperTransition.Fade)));

        var items = await _db.Wallpapers.AsNoTracking()
            .Where(w => w.Active)
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Name)
            .Select(w => new WallpaperItemDto(
                w.Id, w.Name, w.ContentType, w.Extension, w.Sha256,
                w.Width, w.Height, w.SizeBytes, w.UpdatedUtc))
            .ToListAsync();

        var revision = $"{seconds}:{order}:{transition}:" +
                       string.Join("|", items.Select(i => $"{i.Id}:{i.UpdatedUtc.Ticks}"));

        return new WallpaperPlaylistDto(seconds, order, transition, items, revision);
    }
}
