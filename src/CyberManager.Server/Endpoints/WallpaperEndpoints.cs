using System.Security.Cryptography;
using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

/// <summary>Galería de fondos y configuración de reproducción de las estaciones.</summary>
public static class WallpaperEndpoints
{
    public static IEndpointRouteBuilder MapWallpaperEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/wallpapers").WithTags("Fondos de pantalla")
            .RequireAuthorization("Admin");

        g.MapGet("/", async (AppDbContext db) =>
        {
            var list = await db.Wallpapers.AsNoTracking()
                .OrderBy(w => w.SortOrder)
                .ThenBy(w => w.Name)
                .ToListAsync();
            return Results.Ok(list.Select(ToDto).ToList());
        });

        g.MapGet("/playlist", async (WallpaperService wallpapers) =>
            Results.Ok(await wallpapers.PlaylistAsync()));

        g.MapGet("/{id:int}/file", async (int id, AppDbContext db) =>
        {
            var wallpaper = await db.Wallpapers.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id)
                            ?? throw AppException.NotFound("Fondo de pantalla");
            return Results.File(wallpaper.Data, wallpaper.ContentType,
                fileDownloadName: $"{wallpaper.Id}{wallpaper.Extension}", enableRangeProcessing: true);
        });

        // Subida multipart: campo "file" y, opcionalmente, "name" y "sortOrder".
        g.MapPost("/", async (HttpRequest request, AppDbContext db) =>
        {
            if (!request.HasFormContentType)
                throw new AppException("Debe enviar la imagen como formulario multipart.");

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                throw new AppException("Seleccione una imagen para subir.");
            if (file.Length > ImageInspector.MaxBytes)
                throw new AppException($"La imagen supera el máximo de {ImageInspector.MaxBytes / 1024 / 1024} MB.");

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var data = stream.ToArray();

            var probe = ImageInspector.Detect(data);
            if (!probe.Valid)
                throw new AppException("Formato no válido. Use PNG, JPG, GIF o BMP.");

            var name = form["name"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(file.FileName);

            var sortText = form["sortOrder"].ToString();
            var nextOrder = (await db.Wallpapers.MaxAsync(w => (int?)w.SortOrder) ?? 0) + 1;
            if (!int.TryParse(sortText, out var sortOrder)) sortOrder = nextOrder;

            var wallpaper = new Wallpaper
            {
                Name = name,
                ContentType = probe.ContentType,
                Extension = probe.Extension,
                Data = data,
                Width = probe.Width,
                Height = probe.Height,
                SizeBytes = data.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
                Active = true,
                SortOrder = sortOrder,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            db.Wallpapers.Add(wallpaper);
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(wallpaper));
        }).DisableAntiforgery();

        // Reemplazo del archivo de un fondo existente (sincronización desde la carpeta local).
        g.MapPut("/{id:int}/file", async (int id, HttpRequest request, AppDbContext db) =>
        {
            if (!request.HasFormContentType)
                throw new AppException("Debe enviar la imagen como formulario multipart.");

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                throw new AppException("Seleccione una imagen para subir.");
            if (file.Length > ImageInspector.MaxBytes)
                throw new AppException($"La imagen supera el máximo de {ImageInspector.MaxBytes / 1024 / 1024} MB.");

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var data = stream.ToArray();

            var probe = ImageInspector.Detect(data);
            if (!probe.Valid)
                throw new AppException("Formato no válido. Use PNG, JPG, GIF o BMP.");

            var wallpaper = await db.Wallpapers.FirstOrDefaultAsync(w => w.Id == id)
                            ?? throw AppException.NotFound("Fondo de pantalla");

            wallpaper.Data = data;
            wallpaper.ContentType = probe.ContentType;
            wallpaper.Extension = probe.Extension;
            wallpaper.Width = probe.Width;
            wallpaper.Height = probe.Height;
            wallpaper.SizeBytes = data.LongLength;
            wallpaper.Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

            var name = form["name"].ToString().Trim();
            if (!string.IsNullOrWhiteSpace(name))
                wallpaper.Name = name;

            if (int.TryParse(form["sortOrder"].ToString(), out var sortOrder))
                wallpaper.SortOrder = sortOrder;
            if (bool.TryParse(form["active"].ToString(), out var active))
                wallpaper.Active = active;

            wallpaper.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(wallpaper));
        }).DisableAntiforgery();

        g.MapPut("/{id:int}", async (int id, SaveWallpaperRequest req, AppDbContext db) =>
        {
            var wallpaper = await db.Wallpapers.FirstOrDefaultAsync(w => w.Id == id)
                            ?? throw AppException.NotFound("Fondo de pantalla");

            if (string.IsNullOrWhiteSpace(req.Name))
                throw new AppException("Indique un nombre para el fondo.");

            wallpaper.Name = req.Name.Trim();
            wallpaper.Active = req.Active;
            wallpaper.SortOrder = req.SortOrder;
            wallpaper.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(wallpaper));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var wallpaper = await db.Wallpapers.FirstOrDefaultAsync(w => w.Id == id)
                            ?? throw AppException.NotFound("Fondo de pantalla");

            db.Wallpapers.Remove(wallpaper);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // Guarda tiempo, orden y transición en una sola operación. Los equipos
        // tomarán los valores cuando consulten /api/station/state.
        g.MapPost("/apply", async (ApplyWallpaperSettingsRequest req, SettingsService settings,
            WallpaperService wallpapers) =>
        {
            var seconds = Math.Clamp(req.RotationSeconds, WallpaperSettings.MinRotationSeconds,
                WallpaperSettings.MaxRotationSeconds);

            await settings.SetManyAsync(new[]
            {
                new KeyValuePair<string, string>(WallpaperSettings.RotationSeconds,
                    WallpaperSettings.Invariant(seconds)),
                new KeyValuePair<string, string>(WallpaperSettings.Order, req.Order.ToString()),
                new KeyValuePair<string, string>(WallpaperSettings.Transition, req.Transition.ToString())
            });

            return Results.Ok(await wallpapers.PlaylistAsync());
        });

        return app;
    }

    private static WallpaperDto ToDto(Wallpaper w) => new(
        w.Id, w.Name, w.ContentType, w.Extension, w.SizeBytes, w.Width, w.Height,
        w.Sha256, w.Active, w.SortOrder, w.CreatedUtc, w.UpdatedUtc);
}
