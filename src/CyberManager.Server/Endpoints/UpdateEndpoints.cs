using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Endpoints;

/// <summary>Consulta de nuevas versiones publicadas en GitHub.</summary>
public static class UpdateEndpoints
{
    public static IEndpointRouteBuilder MapUpdateEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/updates").WithTags("Actualizaciones").RequireAuthorization();

        g.MapGet("/latest", async (bool? refresh, UpdateReminderService updates, CancellationToken ct) =>
            Results.Ok(await updates.GetStatusAsync(refresh == true, ct)));

        g.MapPost("/dismiss", async (DismissUpdateRequest request, UpdateReminderService updates,
            CancellationToken ct) =>
        {
            await updates.DismissAsync(request.Version);
            return Results.Ok(await updates.GetStatusAsync(false, ct));
        });

        return app;
    }
}
