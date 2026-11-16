using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

/// <summary>Escaneo de la red local y alta automática de las estaciones encontradas.</summary>
public static class NetworkScanEndpoints
{
    public static IEndpointRouteBuilder MapNetworkScanEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stations").WithTags("Equipos").RequireAuthorization("Admin");

        g.MapPost("/scan", async (AppDbContext db, NetworkScanService scan, CancellationToken ct) =>
        {
            var found = await scan.ScanAsync(ct);
            var stations = await db.Stations.AsNoTracking().ToListAsync(ct);

            var results = found.Select(message =>
            {
                var match = FindMatch(stations, message);
                var needsToken = match is null
                                 || string.IsNullOrWhiteSpace(match.DeviceToken)
                                 || string.IsNullOrWhiteSpace(message.Token)
                                 || !string.Equals(match.DeviceToken, message.Token, StringComparison.Ordinal)
                                 || (message.StationId > 0 && message.StationId != match.Id);

                return new DiscoveredStationDto(
                    string.IsNullOrWhiteSpace(message.Role) ? "client" : message.Role!,
                    message.StationId,
                    message.Token,
                    message.ServerUrl ?? string.Empty,
                    string.IsNullOrWhiteSpace(message.MachineName) ? "Equipo" : message.MachineName!,
                    message.IpAddress ?? string.Empty,
                    message.MacAddress ?? string.Empty,
                    message.Version ?? string.Empty,
                    message.SentUtc ?? DateTime.UtcNow,
                    match?.Id,
                    match?.Name,
                    match is null,
                    needsToken);
            }).ToList();

            return Results.Ok(results);
        });

        g.MapPost("/scan/apply", async (ApplyDiscoveryRequest request, HttpRequest http,
            AppDbContext db, NetworkScanService scan, RealtimeService realtime, CancellationToken ct) =>
        {
            var items = request.Stations?
                .Where(i => !string.IsNullOrWhiteSpace(i.IpAddress))
                .ToList() ?? new List<DiscoveryApplyItem>();

            if (items.Count == 0)
                throw new AppException("Seleccione al menos un equipo encontrado en la red.");

            var serverUrl = StationEndpoints.ServerUrlFor(http);
            var stations = await db.Stations.ToListAsync(ct);
            var nextSort = (stations.Count == 0 ? 0 : stations.Max(s => s.SortOrder)) + 1;

            var created = 0;
            var updated = 0;
            var configured = 0;
            var messages = new List<string>();

            foreach (var item in items)
            {
                var station = FindMatch(stations, item);
                var isNew = station is null;

                if (station is null)
                {
                    station = new Station
                    {
                        Name = await UniqueNameAsync(db, item.MachineName, ct),
                        Status = StationStatus.Offline,
                        IsLocked = true,
                        Active = true,
                        SortOrder = nextSort++
                    };
                    db.Stations.Add(station);
                    stations.Add(station);
                }

                station.IpAddress = string.IsNullOrWhiteSpace(item.IpAddress) ? station.IpAddress : item.IpAddress;
                if (!string.IsNullOrWhiteSpace(item.MacAddress)) station.MacAddress = item.MacAddress;
                if (!string.IsNullOrWhiteSpace(item.Version)) station.Version = item.Version;
                station.LastSeenUtc = DateTime.UtcNow;
                station.Active = true;

                if (string.IsNullOrWhiteSpace(station.DeviceToken))
                {
                    station.DeviceToken = string.IsNullOrWhiteSpace(item.Token)
                        ? Guid.NewGuid().ToString("N")
                        : item.Token!;
                }

                await db.SaveChangesAsync(ct);

                if (isNew) created++;
                else updated++;

                var role = string.Equals(item.Role, "agent", StringComparison.OrdinalIgnoreCase)
                    ? "agent"
                    : "client";
                var port = role == "agent" ? DiscoveryProtocol.AgentPort : DiscoveryProtocol.ClientPort;

                var sent = await scan.SendConfigureAsync(item.IpAddress, port, new DiscoveryMessage(
                    DiscoveryProtocol.ConfigureMagic,
                    Role: role,
                    StationId: station.Id,
                    Token: station.DeviceToken,
                    ServerUrl: serverUrl,
                    MachineName: item.MachineName,
                    IpAddress: item.IpAddress,
                    MacAddress: item.MacAddress,
                    Version: item.Version,
                    SentUtc: DateTime.UtcNow), ct);

                if (sent)
                {
                    configured++;
                    messages.Add($"{station.Name}: token y conexión enviados ({role}).");
                }
                else
                {
                    messages.Add($"{station.Name}: no se pudo enviar la configuración a {item.IpAddress}.");
                }
            }

            await realtime.BroadcastEventAsync("StationsScanned",
                $"Escaneo de red aplicado: {created} nuevos, {updated} actualizados");

            return Results.Ok(new DiscoveryApplyResult(created, updated, configured, messages));
        });

        return app;
    }

    private static Station? FindMatch(List<Station> stations, DiscoveryMessage message)
        => FindMatch(stations, message.StationId, message.IpAddress, message.MacAddress);

    private static Station? FindMatch(List<Station> stations, DiscoveryApplyItem item)
        => FindMatch(stations, item.StationId, item.IpAddress, item.MacAddress);

    private static Station? FindMatch(List<Station> stations, int stationId, string? ip, string? mac)
    {
        if (stationId > 0)
        {
            var byId = stations.FirstOrDefault(s => s.Id == stationId);
            if (byId is not null) return byId;
        }

        var normalizedMac = NormalizeMac(mac);
        if (normalizedMac.Length > 0)
        {
            var byMac = stations.FirstOrDefault(s => NormalizeMac(s.MacAddress) == normalizedMac);
            if (byMac is not null) return byMac;
        }

        if (!string.IsNullOrWhiteSpace(ip))
        {
            var byIp = stations.FirstOrDefault(s =>
                string.Equals(s.IpAddress, ip, StringComparison.OrdinalIgnoreCase));
            if (byIp is not null) return byIp;
        }

        return null;
    }

    private static string NormalizeMac(string? mac)
        => (mac ?? string.Empty).Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();

    private static async Task<string> UniqueNameAsync(AppDbContext db, string? desired, CancellationToken ct)
    {
        var baseName = string.IsNullOrWhiteSpace(desired) ? "Equipo" : desired.Trim();
        if (baseName.Length > 80) baseName = baseName[..80];

        var name = baseName;
        var suffix = 2;
        while (await db.Stations.AnyAsync(s => s.Name == name, ct))
            name = $"{baseName} ({suffix++})";

        return name;
    }
}
