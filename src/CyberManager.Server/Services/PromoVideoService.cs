using CyberManager.Server.Data;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>Construye la lista de videos de promoción que reciben los equipos bloqueados.</summary>
public class PromoVideoService
{
    private readonly AppDbContext _db;

    public PromoVideoService(AppDbContext db) => _db = db;

    public async Task<PromoVideoPlaylistDto> PlaylistAsync()
    {
        var items = await _db.PromoVideos.AsNoTracking()
            .Where(v => v.Active)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .Select(v => new PromoVideoItemDto(
                v.Id, v.Name, v.ContentType, v.Extension, v.Sha256,
                v.SizeBytes, v.UpdatedUtc))
            .ToListAsync();

        var revision = string.Join("|", items.Select(i => $"{i.Id}:{i.UpdatedUtc.Ticks}:{i.Sha256}"));
        return new PromoVideoPlaylistDto(items, revision);
    }
}
