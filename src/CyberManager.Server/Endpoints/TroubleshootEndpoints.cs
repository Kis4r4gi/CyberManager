using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Endpoints;

/// <summary>Diagnóstico automático del servidor, los equipos y los registros.</summary>
public static class TroubleshootEndpoints
{
    public static IEndpointRouteBuilder MapTroubleshootEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/troubleshoot")
            .WithTags("Solucionador de problemas")
            .RequireAuthorization("Admin");

        g.MapPost("/diagnose", async (TroubleshootService service,
            TroubleshootRequestDto request, CancellationToken ct) =>
            Results.Ok(await service.DiagnoseAsync(request, ct)));

        return app;
    }
}
