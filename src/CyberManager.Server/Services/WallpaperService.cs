using CyberManager.Server.Data;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>Construye la lista de fondos que deben mostrar los equipos bloqueados.</summary>
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
        var mode = WallpaperSettings.ParseMode(
            await _settings.GetAsync(WallpaperSettings.Mode, nameof(WallpaperMode.None)));

        var seconds = await _settings.GetIntAsync(
            WallpaperSettings.RotationSeconds, WallpaperSettings.DefaultRotationSeconds);
        seconds = Math.Clamp(seconds, WallpaperSettings.MinRotationSeconds,
            WallpaperSettings.MaxRotationSeconds);

        var activeIdText = await _settings.GetAsync(WallpaperSettings.ActiveId);
        int? activeId = int.TryParse(activeIdText, out var parsedId) ? parsedId : null;

        var active = await _db.Wallpapers.AsNoTracking()
            .Where(w => w.Active)
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Name)
            .ToListAsync();

        var selected = new List<Wallpaper>();

        switch (mode)
        {
            case WallpaperMode.Single:
                var chosen = active.FirstOrDefault(w => w.Id == activeId) ?? active.FirstOrDefault();
                if (chosen is null)
                {
                    activeId = null;
                }
                else
                {
                    activeId = chosen.Id;
                    selected.Add(chosen);
                }
                break;

            case WallpaperMode.Slideshow:
                selected.AddRange(active);
                if (activeId is not null && active.All(w => w.Id != activeId))
                    activeId = active.FirstOrDefault()?.Id;
                break;

            default:
                mode = WallpaperMode.None;
                activeId = null;
                break;
        }

        var items = selected.Select(w => new WallpaperItemDto(
            w.Id, w.Name, w.ContentType, w.Extension, w.Sha256,
            w.Width, w.Height, w.SizeBytes, w.UpdatedUtc)).ToList();

        var revision = $"{mode}:{seconds}:{activeId}:" +
                       string.Join("|", items.Select(i => $"{i.Id}:{i.UpdatedUtc.Ticks}"));

        return new WallpaperPlaylistDto(mode, seconds, activeId, items, revision);
    }
}
