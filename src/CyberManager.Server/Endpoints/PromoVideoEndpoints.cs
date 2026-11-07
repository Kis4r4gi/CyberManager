using System.Security.Cryptography;
using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;
using CyberManager.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

/// <summary>Galería de videos promocionales y su sincronización con los terminales.</summary>
public static class PromoVideoEndpoints
{
    public static IEndpointRouteBuilder MapPromoVideoEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/promo-videos").WithTags("Videos promocionales")
            .RequireAuthorization("Admin");

        g.MapGet("/", async (AppDbContext db) =>
        {
            var list = await db.PromoVideos.AsNoTracking()
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.Name)
                .ToListAsync();
            return Results.Ok(list.Select(ToDto).ToList());
        });

        g.MapGet("/playlist", async (PromoVideoService promos) =>
            Results.Ok(await promos.PlaylistAsync()));

        g.MapGet("/{id:int}/file", async (int id, AppDbContext db) =>
        {
            var video = await db.PromoVideos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id)
                        ?? throw AppException.NotFound("Video promocional");
            return Results.File(video.Data, video.ContentType,
                fileDownloadName: $"{video.Id}{video.Extension}", enableRangeProcessing: true);
        });

        // Subida multipart: campo "file" y, opcionalmente, "name" y "sortOrder".
        g.MapPost("/", async (HttpRequest request, AppDbContext db, PromoVideoService promos,
            RealtimeService realtime) =>
        {
            if (!request.HasFormContentType)
                throw new AppException("Debe enviar el video como formulario multipart.");

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                throw new AppException("Seleccione un video para subir.");
            if (file.Length > PromoSettings.MaxBytes)
                throw new AppException($"El video supera el máximo de {PromoSettings.MaxBytes / 1024 / 1024} MB.");

            var extension = Path.GetExtension(file.FileName);
            if (!PromoSettings.IsSupported(file.FileName))
                throw new AppException("Formato no válido. Use MP4, M4V, WMV o AVI.");

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var data = stream.ToArray();

            var name = form["name"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(file.FileName);

            var sortText = form["sortOrder"].ToString();
            var nextOrder = (await db.PromoVideos.MaxAsync(v => (int?)v.SortOrder) ?? 0) + 1;
            if (!int.TryParse(sortText, out var sortOrder)) sortOrder = nextOrder;

            var video = new PromoVideo
            {
                Name = name,
                ContentType = PromoSettings.ContentType(extension),
                Extension = extension.ToLowerInvariant(),
                Data = data,
                SizeBytes = data.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
                Active = true,
                SortOrder = sortOrder,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            db.PromoVideos.Add(video);
            await db.SaveChangesAsync();

            await realtime.PromoVideosChangedAsync(await promos.PlaylistAsync());
            return Results.Ok(ToDto(video));
        }).DisableAntiforgery();

        g.MapPut("/{id:int}", async (int id, SavePromoVideoRequest req, AppDbContext db,
            PromoVideoService promos, RealtimeService realtime) =>
        {
            var video = await db.PromoVideos.FirstOrDefaultAsync(v => v.Id == id)
                        ?? throw AppException.NotFound("Video promocional");

            if (string.IsNullOrWhiteSpace(req.Name))
                throw new AppException("Indique un nombre para el video.");

            video.Name = req.Name.Trim();
            video.Active = req.Active;
            video.SortOrder = req.SortOrder;
            video.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await realtime.PromoVideosChangedAsync(await promos.PlaylistAsync());
            return Results.Ok(ToDto(video));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db,
            PromoVideoService promos, RealtimeService realtime) =>
        {
            var video = await db.PromoVideos.FirstOrDefaultAsync(v => v.Id == id)
                        ?? throw AppException.NotFound("Video promocional");

            db.PromoVideos.Remove(video);
            await db.SaveChangesAsync();

            await realtime.PromoVideosChangedAsync(await promos.PlaylistAsync());
            return Results.NoContent();
        });

        // Reenvía la lista a todos los terminales sin cambiar la configuración.
        g.MapPost("/refresh", async (PromoVideoService promos, RealtimeService realtime) =>
        {
            var playlist = await promos.PlaylistAsync();
            await realtime.PromoVideosChangedAsync(playlist);
            return Results.Ok(playlist);
        });

        return app;
    }

    private static PromoVideoDto ToDto(PromoVideo v) => new(
        v.Id, v.Name, v.ContentType, v.Extension, v.SizeBytes, v.Sha256,
        v.Active, v.SortOrder, v.CreatedUtc, v.UpdatedUtc);
}
