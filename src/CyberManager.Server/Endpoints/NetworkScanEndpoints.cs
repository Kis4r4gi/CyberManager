using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Endpoints;

/// <summary>Escaneo de la red local y alta automática de las estaciones encontradas.</summary>
public static class NetworkScanEndpoints
{
    public static IEndpointRouteBuilder MapNetworkScanEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stations").WithTags("Equipos").RequireAuthorization("Admin");

        g.MapPost("/scan", async (StationDiscoveryService discovery, CancellationToken ct)
            => Results.Ok(await discovery.ScanAsync(ct)));

        g.MapPost("/scan/apply", async (ApplyDiscoveryRequest request, HttpRequest http,
            StationDiscoveryService discovery, CancellationToken ct) =>
        {
            var items = request.Stations?
                .Where(i => !string.IsNullOrWhiteSpace(i.IpAddress))
                .ToList() ?? new List<DiscoveryApplyItem>();

            return Results.Ok(await discovery.ApplyAsync(items,
                StationEndpoints.ServerUrlFor(http), automatic: false, ct));
        });

        return app;
    }
}
