using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Endpoints;

/// <summary>Exportación, restauración y respaldo automático de la base de datos.</summary>
public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/backups").WithTags("Respaldos").RequireAuthorization("Admin");

        g.MapGet("/", async (BackupService backups, CancellationToken ct) =>
            Results.Ok(await backups.GetListAsync(ct)));

        g.MapPost("/", async (BackupService backups, CancellationToken ct) =>
            Results.Ok(await backups.CreateAsync(automatic: false, ct)));

        g.MapGet("/{fileName}/download", async (string fileName, BackupService backups,
            CancellationToken ct) =>
        {
            var list = await backups.ListFilesAsync(ct);
            var file = list.FirstOrDefault(f =>
                           string.Equals(f.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                       ?? throw AppException.NotFound("Respaldo");
            var path = Path.Combine(backups.Folder, file.FileName);
            return Results.File(path, "application/octet-stream", file.FileName);
        });

        g.MapPost("/{fileName}/restore", async (string fileName, BackupService backups,
            CancellationToken ct) =>
            Results.Ok(await backups.RestoreStoredAsync(fileName, ct)));

        g.MapPost("/restore-upload", async (HttpRequest request, BackupService backups,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                throw new AppException("Debe enviar el archivo de respaldo como formulario multipart.");

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                throw new AppException("Seleccione el archivo de respaldo que desea restaurar.");

            await using var stream = file.OpenReadStream();
            return Results.Ok(await backups.RestoreUploadAsync(stream, file.FileName, ct));
        }).DisableAntiforgery();

        return app;
    }
}
