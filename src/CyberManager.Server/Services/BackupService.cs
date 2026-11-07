using System.Globalization;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.Data.Sqlite;

namespace CyberManager.Server.Services;

/// <summary>Ubicación física de la base de datos activa.</summary>
public sealed record DatabaseInfo(string FilePath);

/// <summary>
/// Respaldos de la base SQLite usando la API de copia en línea.
/// Los archivos se guardan en la carpeta <c>respaldos</c> junto a la base de datos.
/// </summary>
public sealed class BackupService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly DatabaseInfo _database;
    private readonly SettingsService _settings;
    private readonly ILogger<BackupService> _log;

    public BackupService(DatabaseInfo database, SettingsService settings, ILogger<BackupService> log)
    {
        _database = database;
        _settings = settings;
        _log = log;
    }

    public string DatabaseFile => _database.FilePath;

    public string Folder =>
        Path.Combine(Path.GetDirectoryName(_database.FilePath) ?? AppContext.BaseDirectory, "respaldos");

    public async Task<BackupListDto> GetListAsync(CancellationToken ct = default)
    {
        var enabled = await _settings.GetBoolAsync(SettingsKeys.BackupAutoEnabled, true);
        var interval = Math.Clamp(await _settings.GetIntAsync(SettingsKeys.BackupIntervalHours, 24), 1, 720);
        var retention = Math.Clamp(await _settings.GetIntAsync(SettingsKeys.BackupRetention, 30), 1, 365);
        var files = await ListFilesAsync(ct);

        return new BackupListDto(Folder, enabled, interval, retention,
            files.FirstOrDefault()?.CreatedUtc, files);
    }

    public async Task<List<BackupFileDto>> ListFilesAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(Folder)) return new List<BackupFileDto>();

        return await Task.Run(() =>
        {
            var list = new List<BackupFileDto>();
            foreach (var path in Directory.EnumerateFiles(Folder, "*.db"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(path);
                    var name = info.Name;
                    list.Add(new BackupFileDto(
                        name,
                        info.Length,
                        info.LastWriteTimeUtc,
                        name.Contains("_auto", StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudo leer el respaldo {File}", path);
                }
            }

            return list.OrderByDescending(f => f.CreatedUtc).ToList();
        }, ct);
    }

    public async Task<BackupFileDto> CreateAsync(bool automatic, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await CreateCoreAsync(automatic, ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Crea un respaldo automático si ya venció el intervalo configurado.</summary>
    public async Task<bool> RunAutoIfDueAsync(CancellationToken ct = default)
    {
        var enabled = await _settings.GetBoolAsync(SettingsKeys.BackupAutoEnabled, true);
        if (!enabled) return false;

        var interval = Math.Clamp(await _settings.GetIntAsync(SettingsKeys.BackupIntervalHours, 24), 1, 720);
        var files = await ListFilesAsync(ct);
        var last = files.Where(f => f.Automatic).OrderByDescending(f => f.CreatedUtc).FirstOrDefault();
        if (last is not null && DateTime.UtcNow - last.CreatedUtc < TimeSpan.FromHours(interval))
            return false;

        await Gate.WaitAsync(ct);
        try
        {
            await CreateCoreAsync(true, ct);
            await PruneCoreAsync(ct);
        }
        finally
        {
            Gate.Release();
        }

        return true;
    }

    public async Task<RestoreBackupResultDto> RestoreStoredAsync(string fileName, CancellationToken ct = default)
    {
        var path = StoredPath(fileName);
        if (!File.Exists(path)) throw AppException.NotFound("Respaldo");

        return await RestoreFromFileAsync(path, Path.GetFileName(path), ct);
    }

    public async Task<RestoreBackupResultDto> RestoreUploadAsync(Stream source, string originalName,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Folder);
        var temp = Path.Combine(Folder, $".upload_{Guid.NewGuid():N}.db");

        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             64 * 1024, useAsync: true))
            {
                await source.CopyToAsync(file, ct);
            }

            await ValidateDatabaseAsync(temp, ct);
            return await RestoreFromFileAsync(temp, string.IsNullOrWhiteSpace(originalName)
                ? "archivo_subido.db"
                : Path.GetFileName(originalName), ct);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private async Task<RestoreBackupResultDto> RestoreFromFileAsync(string sourcePath, string displayName,
        CancellationToken ct)
    {
        await ValidateDatabaseAsync(sourcePath, ct);

        await Gate.WaitAsync(ct);
        try
        {
            // Respaldo de seguridad antes de sobrescribir la base activa.
            var safety = await CreateCoreAsync(false, ct, "antes_restaurar");

            SqliteConnection.ClearAllPools();
            await Task.Run(() =>
            {
                using var source = Open(sourcePath);
                using var destination = Open(_database.FilePath);
                source.BackupDatabase(destination);
            }, ct);
            SqliteConnection.ClearAllPools();

            _log.LogInformation("Base de datos restaurada desde {File}", displayName);
            return new RestoreBackupResultDto(displayName, safety.FileName, DateTime.UtcNow);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<BackupFileDto> CreateCoreAsync(bool automatic, CancellationToken ct, string? kind = null)
    {
        Directory.CreateDirectory(Folder);
        kind ??= automatic ? "auto" : "manual";
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var path = UniquePath(Path.Combine(Folder, $"cybermanager_{stamp}_{kind}.db"));

        await Task.Run(() =>
        {
            using var source = Open(_database.FilePath, readOnly: true);
            using var destination = Open(path);
            source.BackupDatabase(destination);
        }, ct);

        var info = new FileInfo(path);
        _log.LogInformation("Respaldo creado: {File} ({Bytes} bytes)", info.Name, info.Length);

        return new BackupFileDto(info.Name, info.Length, info.LastWriteTimeUtc, automatic);
    }

    private async Task PruneCoreAsync(CancellationToken ct)
    {
        var retention = Math.Clamp(await _settings.GetIntAsync(SettingsKeys.BackupRetention, 30), 1, 365);
        var files = await ListFilesAsync(ct);

        foreach (var old in files.Where(f => f.Automatic).Skip(retention))
        {
            try
            {
                File.Delete(Path.Combine(Folder, old.FileName));
                _log.LogInformation("Respaldo automático antiguo eliminado: {File}", old.FileName);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo eliminar el respaldo {File}", old.FileName);
            }
        }
    }

    private string StoredPath(string fileName)
    {
        fileName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            throw new AppException("Nombre de respaldo no válido.");

        var full = Path.GetFullPath(Path.Combine(Folder, fileName));
        var root = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new AppException("Nombre de respaldo no válido.");

        return full;
    }

    private static async Task ValidateDatabaseAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, true);
        var header = new byte[16];
        if (await stream.ReadAsync(header, ct) < 16 ||
            System.Text.Encoding.ASCII.GetString(header) != "SQLite format 3\0")
            throw new AppException("El archivo seleccionado no es una base de datos SQLite válida.");

        await Task.Run(() =>
        {
            using var connection = Open(path, readOnly: true);
            using (var check = connection.CreateCommand())
            {
                check.CommandText = "PRAGMA integrity_check;";
                var result = check.ExecuteScalar()?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new AppException("La base de datos está dañada y no se puede restaurar.");
            }

            using (var tables = connection.CreateCommand())
            {
                tables.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Settings','Stations','Users');";
                var count = Convert.ToInt64(tables.ExecuteScalar());
                if (count < 3)
                    throw new AppException("El archivo no corresponde a una base de datos de CyberManager.");
            }
        }, ct);
    }

    private static SqliteConnection Open(string path, bool readOnly = false)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}.db");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Un archivo temporal que no se puede borrar no debe romper la operación.
        }
    }
}

/// <summary>Ejecuta el respaldo automático mientras el servidor está en marcha.</summary>
public sealed class BackupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BackupWorker> _log;

    public BackupWorker(IServiceScopeFactory scopes, ILogger<BackupWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
                if (await backups.RunAutoIfDueAsync(stoppingToken))
                    _log.LogInformation("Respaldo automático completado.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "No se pudo ejecutar el respaldo automático.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
