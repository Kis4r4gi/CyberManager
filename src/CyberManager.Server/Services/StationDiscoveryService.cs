using CyberManager.Server.Data;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>
/// Lógica compartida por el escaneo manual de la consola y el descubrimiento automático
/// del servidor. La MAC es la identidad principal: una estación puede cambiar de IP,
/// pero no debe dar de alta una fila nueva ni perder su token o su sesión.
/// </summary>
public sealed class StationDiscoveryService(
    AppDbContext db,
    NetworkScanService scanner,
    SettingsService settings,
    RealtimeService realtime,
    ILogger<StationDiscoveryService> log)
{
    public async Task<List<DiscoveredStationDto>> ScanAsync(CancellationToken ct)
    {
        var found = await scanner.ScanAsync(ct);
        var stations = await db.Stations.AsNoTracking().ToListAsync(ct);

        return found.Select(message =>
        {
            var match = FindMatch(stations, message.StationId, message.IpAddress,
                message.MacAddress, message.MachineName);

            // El cliente ya configurado no expone su token por UDP. Eso no significa
            // que haya que reemplazarlo: solo se marca como pendiente cuando no hay
            // coincidencia o la fila no tiene token.
            var needsToken = match is null
                             || string.IsNullOrWhiteSpace(match.DeviceToken)
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
    }

    /// <summary>
    /// Da de alta o actualiza los equipos detectados. En modo automático solo se crean
    /// filas nuevas cuando el equipo reporta MAC: un nombre/IP sin MAC es demasiado
    /// débil y podría duplicar una estación existente.
    /// </summary>
    public async Task<DiscoveryApplyResult> ApplyAsync(IReadOnlyList<DiscoveryApplyItem> source,
        string serverUrl, bool automatic, CancellationToken ct)
    {
        var items = Dedupe(source);
        if (items.Count == 0)
            throw new AppException("Seleccione al menos un equipo encontrado en la red.");

        var stations = await db.Stations.ToListAsync(ct);
        var suppressions = await db.DeletedStationIdentities.ToListAsync(ct);
        var activeCount = stations.Count(s => s.Active);
        var maxStations = await settings.GetIntAsync(SettingsKeys.MaxStations, 0);
        var nextSort = stations.Count == 0 ? 0 : stations.Max(s => s.SortOrder) + 1;

        var created = 0;
        var updated = 0;
        var configured = 0;
        var messages = new List<string>();
        var changed = false;

        foreach (var item in items)
        {
            var itemMac = NormalizeMac(item.MacAddress);

            if (automatic && IsSuppressed(suppressions, item, itemMac))
            {
                messages.Add($"{item.MachineName}: equipo eliminado; no se vuelve a agregar.");
                continue;
            }

            if (!automatic)
            {
                var toClear = suppressions.Where(s =>
                    (itemMac.Length > 0 && NormalizeMac(s.MacAddress) == itemMac) ||
                    (!string.IsNullOrWhiteSpace(item.IpAddress) &&
                     string.Equals(s.IpAddress, item.IpAddress, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (toClear.Count > 0)
                {
                    db.DeletedStationIdentities.RemoveRange(toClear);
                    suppressions.RemoveAll(s => toClear.Contains(s));
                }
            }

            var station = FindMatch(stations, item.StationId, item.IpAddress,
                item.MacAddress, item.MachineName);
            var isNew = station is null;
            var matchedByMac = !isNew && NormalizeMac(item.MacAddress).Length > 0 &&
                               NormalizeMac(station!.MacAddress) == NormalizeMac(item.MacAddress);

            if (station is null)
            {
                if (automatic && NormalizeMac(item.MacAddress).Length == 0)
                {
                    messages.Add($"{item.MachineName}: detectado sin MAC; se omite para no duplicarlo.");
                    continue;
                }

                if (maxStations > 0 && activeCount >= maxStations)
                {
                    messages.Add($"{item.MachineName}: no se agregó; límite de {maxStations} equipos.");
                    continue;
                }

                station = new Station
                {
                    Name = await UniqueNameAsync(item.MachineName, ct),
                    Status = StationStatus.Offline,
                    IsLocked = true,
                    Active = true,
                    SortOrder = nextSort++
                };
                db.Stations.Add(station);
                stations.Add(station);
                activeCount++;
                changed = true;
            }
            else if (!station.Active && automatic && !matchedByMac)
            {
                // Una baja solo se revierte cuando la MAC confirma que es el mismo PC.
                messages.Add($"{station.Name}: detectado pero inactivo; no se reactivó.");
                continue;
            }

            var hadChanges = station.IpAddress != item.IpAddress
                             || (!string.IsNullOrWhiteSpace(item.MacAddress) &&
                                 NormalizeMac(station.MacAddress) != NormalizeMac(item.MacAddress))
                             || (!string.IsNullOrWhiteSpace(item.Version) && station.Version != item.Version)
                             || !station.Active;

            station.IpAddress = item.IpAddress;
            if (!string.IsNullOrWhiteSpace(item.MacAddress)) station.MacAddress = NormalizeMacString(item.MacAddress);
            if (!string.IsNullOrWhiteSpace(item.Version)) station.Version = item.Version;
            station.LastSeenUtc = DateTime.UtcNow;
            station.Active = true;

            if (string.IsNullOrWhiteSpace(station.DeviceToken))
            {
                station.DeviceToken = string.IsNullOrWhiteSpace(item.Token)
                    ? Guid.NewGuid().ToString("N")
                    : item.Token!;
                hadChanges = true;
            }

            // Si otro registro tenía esta IP sin MAC (escaneos antiguos), se da de baja
            // y se le quita la IP para que no vuelva a aparecer como equipo distinto.
            foreach (var duplicate in stations.Where(s => s.Id != station.Id &&
                                                          string.Equals(s.IpAddress, item.IpAddress,
                                                              StringComparison.OrdinalIgnoreCase) &&
                                                          NormalizeMac(s.MacAddress).Length == 0))
            {
                duplicate.Active = false;
                duplicate.IpAddress = null;
                duplicate.LastSeenUtc = null;
                messages.Add($"{duplicate.Name}: duplicado con {station.Name} y dado de baja.");
                hadChanges = true;
            }

            await db.SaveChangesAsync(ct);

            if (isNew) created++;
            else updated++;
            if (hadChanges) changed = true;

            var role = string.Equals(item.Role, "agent", StringComparison.OrdinalIgnoreCase)
                ? "agent"
                : "client";
            var port = role == "agent" ? DiscoveryProtocol.AgentPort : DiscoveryProtocol.ClientPort;

            // Reenviar la configuración repara estaciones con un token equivocado:
            // el cliente solo acepta este paquete si viene de su servidor guardado.
            var sent = await scanner.SendConfigureAsync(item.IpAddress, port, new DiscoveryMessage(
                DiscoveryProtocol.ConfigureMagic,
                Role: role,
                StationId: station.Id,
                Token: station.DeviceToken,
                ServerUrl: serverUrl,
                MachineName: item.MachineName,
                IpAddress: item.IpAddress,
                MacAddress: station.MacAddress,
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

        if (created > 0 || updated > 0)
        {
            await realtime.BroadcastEventAsync("StationsScanned",
                $"Escaneo de red aplicado: {created} nuevos, {updated} actualizados");
        }

        if (changed) log.LogInformation("Descubrimiento: {Created} nuevos, {Updated} actualizados", created, updated);

        return new DiscoveryApplyResult(created, updated, configured, messages);
    }

    /// <summary>
    /// Registra una estación que se anunció al buscar el servidor por UDP 5094.
    /// Funciona aunque el firewall del cliente bloquee las respuestas al escaneo 5092.
    /// </summary>
    public async Task RegisterAnnouncementAsync(DiscoveryMessage message, string sourceIp,
        string serverUrl, CancellationToken ct)
    {
        var itemMac = NormalizeMac(message.MacAddress);
        if (message.StationId <= 0 && itemMac.Length == 0 &&
            string.IsNullOrWhiteSpace(message.MachineName)) return;

        var item = new DiscoveryApplyItem(
            string.IsNullOrWhiteSpace(message.Role) ? "client" : message.Role!,
            message.StationId, message.Token,
            string.IsNullOrWhiteSpace(message.MachineName) ? "Equipo" : message.MachineName!,
            sourceIp, message.MacAddress ?? string.Empty, message.Version ?? string.Empty);

        var suppressions = await db.DeletedStationIdentities.ToListAsync(ct);
        if (IsSuppressed(suppressions, item, itemMac)) return;

        var stations = await db.Stations.ToListAsync(ct);
        var station = FindMatch(stations, item.StationId, sourceIp, item.MacAddress, item.MachineName);
        var isNew = station is null;

        if (station is null)
        {
            // Sin MAC no se crea una fila nueva: el nombre/IP podrían pertenecer a
            // una estación ya registrada con otra dirección.
            if (itemMac.Length == 0) return;

            var maxStations = await settings.GetIntAsync(SettingsKeys.MaxStations, 0);
            if (maxStations > 0 && stations.Count(s => s.Active) >= maxStations) return;

            station = new Station
            {
                Name = await UniqueNameAsync(item.MachineName, ct),
                Status = StationStatus.Offline,
                IsLocked = true,
                Active = true,
                SortOrder = stations.Count == 0 ? 0 : stations.Max(s => s.SortOrder) + 1
            };
            db.Stations.Add(station);
        }

        var changed = station.IpAddress != sourceIp
                      || (itemMac.Length > 0 && NormalizeMac(station.MacAddress) != itemMac)
                      || (!string.IsNullOrWhiteSpace(item.Version) && station.Version != item.Version)
                      || !station.Active;

        station.IpAddress = sourceIp;
        if (itemMac.Length > 0) station.MacAddress = NormalizeMacString(item.MacAddress);
        if (!string.IsNullOrWhiteSpace(item.Version)) station.Version = item.Version;
        station.LastSeenUtc = DateTime.UtcNow;
        station.Active = true;

        if (string.IsNullOrWhiteSpace(station.DeviceToken))
        {
            station.DeviceToken = string.IsNullOrWhiteSpace(message.Token)
                ? Guid.NewGuid().ToString("N")
                : message.Token!;
            changed = true;
        }

        foreach (var duplicate in stations.Where(s => s.Id != station.Id &&
                                                      string.Equals(s.IpAddress, sourceIp,
                                                          StringComparison.OrdinalIgnoreCase) &&
                                                      NormalizeMac(s.MacAddress).Length == 0))
        {
            duplicate.Active = false;
            duplicate.IpAddress = null;
            duplicate.LastSeenUtc = null;
            log.LogInformation("Duplicado {Name} dado de baja al registrar {Station}",
                duplicate.Name, station.Name);
            changed = true;
        }

        if (!changed) return;
        await db.SaveChangesAsync(ct);

        await realtime.BroadcastEventAsync("StationsScanned",
            $"Estación {station.Name} localizada en la red ({sourceIp}).");

        // Un cliente con token desactualizado no conecta aunque sepa la IP. Solo se
        // reenvía la configuración si no coincide; el cliente la acepta únicamente
        // cuando el paquete viene de su servidor guardado.
        if (!string.IsNullOrWhiteSpace(message.Token) &&
            string.Equals(message.Token, station.DeviceToken, StringComparison.Ordinal)) return;

        var role = string.Equals(message.Role, "agent", StringComparison.OrdinalIgnoreCase)
            ? "agent" : "client";
        var port = role == "agent" ? DiscoveryProtocol.AgentPort : DiscoveryProtocol.ClientPort;
        await scanner.SendConfigureAsync(sourceIp, port, new DiscoveryMessage(
            DiscoveryProtocol.ConfigureMagic,
            Role: role,
            StationId: station.Id,
            Token: station.DeviceToken,
            ServerUrl: serverUrl,
            MachineName: item.MachineName,
            IpAddress: sourceIp,
            MacAddress: station.MacAddress,
            Version: item.Version,
            SentUtc: DateTime.UtcNow), ct);
    }

    /// <summary>
    /// Orden de fiabilidad: MAC, ID, nombre y por último IP. Así un equipo con IP
    /// cambiada se empareja con su fila original en vez de crear un duplicado.
    /// </summary>
    internal static Station? FindMatch(IEnumerable<Station> stations, int stationId,
        string? ip, string? mac, string? name)
    {
        var all = stations.ToList();
        var normalizedMac = NormalizeMac(mac);

        if (normalizedMac.Length > 0)
        {
            var byMac = all.FirstOrDefault(s => NormalizeMac(s.MacAddress) == normalizedMac);
            if (byMac is not null) return byMac;
        }

        if (stationId > 0)
        {
            var byId = all.FirstOrDefault(s => s.Id == stationId);
            if (byId is not null) return byId;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var byName = all.FirstOrDefault(s => string.Equals(s.Name, name.Trim(),
                StringComparison.OrdinalIgnoreCase));
            if (byName is not null) return byName;
        }

        if (!string.IsNullOrWhiteSpace(ip))
        {
            var byIp = all.FirstOrDefault(s => string.Equals(s.IpAddress, ip,
                StringComparison.OrdinalIgnoreCase));
            if (byIp is not null) return byIp;
        }

        return null;
    }

    private static List<DiscoveryApplyItem> Dedupe(IReadOnlyList<DiscoveryApplyItem> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.Where(i => !string.IsNullOrWhiteSpace(i.IpAddress))
            .Where(i => seen.Add(NormalizeMac(i.MacAddress) is { Length: > 0 } mac
                ? "M:" + mac
                : "I:" + i.IpAddress!.Trim()))
            .ToList();
    }

    /// <summary>
    /// Un equipo eliminado con MAC nunca se readmite automáticamente. Las exclusiones
    /// creadas antes de que existiera la MAC solo aplican a esa dirección IP.
    /// </summary>
    private static bool IsSuppressed(IEnumerable<DeletedStationIdentity> suppressions,
        DiscoveryApplyItem item, string itemMac)
    {
        if (itemMac.Length > 0)
            return suppressions.Any(s => NormalizeMac(s.MacAddress) == itemMac);

        return !string.IsNullOrWhiteSpace(item.IpAddress) &&
               suppressions.Any(s => string.Equals(s.IpAddress, item.IpAddress,
                   StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeMac(string? mac)
        => (mac ?? string.Empty).Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();

    private static string NormalizeMacString(string mac)
    {
        var normalized = NormalizeMac(mac);
        return string.IsNullOrEmpty(normalized)
            ? mac.Trim()
            : string.Join(":", Enumerable.Range(0, normalized.Length / 2)
                .Select(i => normalized.Substring(i * 2, 2)));
    }

    private async Task<string> UniqueNameAsync(string? desired, CancellationToken ct)
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
