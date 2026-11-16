using System.IO.Compression;
using System.Security.Cryptography;
using CyberManager.Shared.Dtos;

namespace CyberManager.Shared;

/// <summary>
/// Descarga, verifica y aplica el paquete del cliente. Lo usan el agente (como
/// servicio) y la propia pantalla del cliente cuando tiene permisos de escritura.
/// </summary>
public static class ClientUpdateInstaller
{
    /// <summary>Archivo que la estación detecta para reiniciarse con los binarios nuevos.</summary>
    public const string PendingFile = "update.pending";

    /// <summary>Carpeta de instalación del cliente o null si no se puede determinar.</summary>
    public static string? ResolveClientDir()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(Path.GetFileName(baseDir), "Client", StringComparison.OrdinalIgnoreCase))
            return baseDir;

        var parent = Directory.GetParent(baseDir)?.FullName;
        if (parent is null) return null;

        var sibling = Path.Combine(parent, "Client");
        return Directory.Exists(sibling) ? sibling : null;
    }

    /// <summary>
    /// Descarga el paquete, comprueba el SHA-256 y lo extrae en una carpeta temporal.
    /// Devuelve la ruta de la carpeta extraída o null si falló la verificación.
    /// </summary>
    public static async Task<string?> DownloadAndExtractAsync(string serverUrl,
        ClientUpdateManifestDto manifest, string workDir, HttpClient http, CancellationToken ct)
    {
        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, "client.zip");

        using (var response = await http.GetAsync(
                   $"{serverUrl.TrimEnd('/')}/api/client-update/download",
                   HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(zipPath);
            await source.CopyToAsync(target, ct);
        }

        var hash = ComputeSha256(zipPath);
        if (!string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(zipPath);
            return null;
        }

        var staging = Path.Combine(workDir, "staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        ZipFile.ExtractToDirectory(zipPath, staging, true);
        return staging;
    }

    /// <summary>
    /// Copia los archivos extraídos sobre la instalación. Si un archivo está en uso,
    /// lo renombra a <c>.old</c> y coloca el nuevo; la estación terminará el trabajo
    /// al reiniciarse con el archivo <see cref="PendingFile"/>.
    /// </summary>
    public static bool Apply(string stagingDir, string installDir, out string? error)
    {
        error = null;
        if (!Directory.Exists(stagingDir) || !Directory.Exists(installDir))
        {
            error = "No se encontró la carpeta de instalación del cliente.";
            return false;
        }

        try
        {
            // Restos de una actualización anterior que ya no están en uso.
            foreach (var old in Directory.GetFiles(installDir, "*.old", SearchOption.AllDirectories))
                TryDelete(old);

            foreach (var source in Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(stagingDir, source);
                var destination = Path.Combine(installDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                try
                {
                    File.Copy(source, destination, true);
                }
                catch (IOException)
                {
                    var backup = destination + ".old";
                    TryDelete(backup);
                    File.Move(destination, backup, true);
                    File.Copy(source, destination, true);
                }
                catch (UnauthorizedAccessException)
                {
                    var backup = destination + ".old";
                    TryDelete(backup);
                    File.Move(destination, backup, true);
                    File.Copy(source, destination, true);
                }
            }

            File.WriteAllText(Path.Combine(installDir, PendingFile),
                DateTime.UtcNow.ToString("O"));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string ComputeSha256(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* en uso */ }
    }
}
