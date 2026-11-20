using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;

namespace CyberManager.Server.Endpoints;

/// <summary>Distribución del paquete del cliente que se guarda junto al servidor.</summary>
public static class ClientUpdateEndpoints
{
    public static IEndpointRouteBuilder MapClientUpdateEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/client-update").WithTags("Actualización del cliente");

        // El cliente y el agente consultan el manifiesto sin token: el paquete no
        // contiene datos del negocio, solo los binarios del cliente.
        g.MapGet("/manifest", (ClientUpdateService updates) =>
        {
            var manifest = updates.GetManifest();
            return manifest is null ? Results.NoContent() : Results.Ok(manifest);
        }).AllowAnonymous();

        g.MapGet("/download", (ClientUpdateService updates) =>
        {
            var manifest = updates.GetManifest();
            if (manifest is null || !File.Exists(updates.PackagePath))
                throw AppException.NotFound("Paquete del cliente");

            return Results.File(updates.PackagePath, "application/zip",
                manifest.FileName, enableRangeProcessing: true);
        }).AllowAnonymous();

        // El cliente descarga el agente por este canal cuando el servicio no está
        // registrado o no conecta. Es el mismo paquete que se adjunta a la release.
        g.MapGet("/agent-download", (ClientUpdateService updates) =>
        {
            var manifest = updates.GetManifest();
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.AgentSha256) ||
                !File.Exists(updates.AgentPackagePath))
                throw AppException.NotFound("Paquete del agente");

            return Results.File(updates.AgentPackagePath, "application/zip",
                manifest.AgentFileName ?? "agent.zip", enableRangeProcessing: true);
        }).AllowAnonymous();

        g.MapGet("/status", async (AppDbContext db, ClientUpdateService updates, CancellationToken ct) =>
            Results.Ok(await updates.GetStatusAsync(db, ct)))
            .RequireAuthorization("Admin");

        g.MapPost("/notify", async (int? stationId, ClientUpdateService updates,
            RealtimeService realtime, CancellationToken ct) =>
        {
            var manifest = await updates.NotifyAsync(realtime, ct, stationId);
            if (manifest is null)
                throw new AppException("El servidor no tiene un paquete de cliente para distribuir.", 409);

            return Results.Ok(new
            {
                sent = true,
                stationId,
                version = manifest.Version,
                size = manifest.Size
            });
        }).RequireAuthorization("Admin");

        return app;
    }
}
