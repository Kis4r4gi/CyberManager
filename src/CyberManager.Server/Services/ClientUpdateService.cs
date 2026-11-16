using System.Security.Cryptography;
using System.Text.Json;
using CyberManager.Server.Data;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>
/// Guarda el paquete del cliente que produce el instalador del servidor
/// (<c>ClientUpdate\client.zip</c>) y lo publica a las estaciones. El manifiesto
/// incluye la versión y el SHA-256 para que el cliente verifique la descarga.
/// </summary>
public sealed class ClientUpdateService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _folder;
    private readonly string _manifestFile;
    private readonly string _packageFile;
    private readonly ILogger<ClientUpdateService> _log;
    private readonly object _gate = new();

    private ClientUpdateManifestDto? _cached;
    private DateTime _cachedStamp = DateTime.MinValue;

    public ClientUpdateService(ILogger<ClientUpdateService> log)
    {
        _log = log;
        _folder = Path.Combine(AppContext.BaseDirectory, "ClientUpdate");
        _manifestFile = Path.Combine(_folder, "manifest.json");
        _packageFile = Path.Combine(_folder, "client.zip");
    }

    public string PackagePath => _packageFile;

    /// <summary>Manifiesto vigente o null cuando el servidor no tiene paquete del cliente.</summary>
    public ClientUpdateManifestDto? GetManifest()
    {
        lock (_gate)
        {
            if (!File.Exists(_packageFile))
            {
                _cached = null;
                return null;
            }

            var stamp = File.GetLastWriteTimeUtc(_packageFile);
            if (File.Exists(_manifestFile))
                stamp = Max(stamp, File.GetLastWriteTimeUtc(_manifestFile));

            if (_cached is not null && stamp == _cachedStamp) return _cached;

            ClientUpdateManifestDto? manifest = null;
            if (File.Exists(_manifestFile))
            {
                try
                {
                    manifest = JsonSerializer.Deserialize<ClientUpdateManifestDto>(
                        File.ReadAllText(_manifestFile), Json);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "El manifiesto del cliente no se pudo leer; se regenerará.");
                }
            }

            // Si falta el manifiesto se genera con la versión del servidor.
            manifest ??= BuildManifest(AppInfo.Version);

            if (manifest is null) return null;

            if (!string.Equals(manifest.FileName, Path.GetFileName(_packageFile), StringComparison.OrdinalIgnoreCase))
                manifest = manifest with { FileName = Path.GetFileName(_packageFile) };

            _cached = manifest;
            _cachedStamp = stamp;
            return manifest;
        }
    }

    public async Task<ClientUpdateStatusDto> GetStatusAsync(AppDbContext db, CancellationToken ct)
    {
        var manifest = GetManifest();
        var versions = await db.Stations.AsNoTracking()
            .Where(s => s.Active)
            .Select(s => new { s.Version })
            .ToListAsync(ct);

        var stations = versions.Count;
        var upToDate = 0;
        var outdated = 0;
        var unknown = 0;

        if (manifest is not null)
        {
            foreach (var row in versions)
            {
                if (string.IsNullOrWhiteSpace(row.Version))
                {
                    unknown++;
                    continue;
                }

                if (CompareVersions(row.Version!, manifest.Version) < 0) outdated++;
                else upToDate++;
            }
        }

        var message = manifest is null
            ? "El servidor no tiene un paquete de cliente. Publique la versión con el instalador."
            : stations == 0
                ? "No hay equipos activos registrados."
                : upToDate == 0 && outdated == 0
                    ? "Ningún equipo ha reportado su versión todavía."
                    : outdated == 0
                        ? "Todos los equipos que reportaron están al día."
                        : $"{outdated} equipos tienen una versión anterior del cliente.";

        return new ClientUpdateStatusDto(
            AppInfo.Version,
            manifest?.Version,
            manifest is not null,
            manifest?.Size ?? 0,
            manifest?.Sha256,
            manifest?.PublishedUtc,
            stations,
            upToDate,
            outdated,
            unknown,
            message);
    }

    public async Task<ClientUpdateManifestDto?> NotifyAsync(RealtimeService realtime, CancellationToken ct)
    {
        var manifest = GetManifest();
        if (manifest is null) return null;

        await realtime.ClientUpdateAvailableAsync(manifest);
        return manifest;
    }

    private ClientUpdateManifestDto? BuildManifest(string version)
    {
        try
        {
            var info = new FileInfo(_packageFile);
            var hash = ComputeSha256(_packageFile);
            var manifest = new ClientUpdateManifestDto(
                version, info.Name, info.Length, hash, DateTime.UtcNow);

            Directory.CreateDirectory(_folder);
            File.WriteAllText(_manifestFile, JsonSerializer.Serialize(manifest, Json));
            return manifest;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo generar el manifiesto del cliente.");
            return null;
        }
    }

    private static string ComputeSha256(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    /// <summary>Compara dos versiones numéricas; devuelve negativo si left &lt; right.</summary>
    public static int CompareVersions(string left, string right) => VersionUtil.Compare(left, right);
}
