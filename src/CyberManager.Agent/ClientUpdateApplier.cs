using System.Net.Http.Json;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Agent;

/// <summary>
/// Mantiene la pantalla del cliente al día. El agente corre como SYSTEM, así que
/// puede reemplazar los archivos de <c>..\Client</c> y dejar una marca para que la
/// estación se reinicie con la versión nueva.
/// </summary>
public sealed class ClientUpdateApplier
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    private readonly ILogger<ClientUpdateApplier> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastCheck = DateTime.MinValue;
    private string _lastAppliedVersion = string.Empty;

    public ClientUpdateApplier(ILogger<ClientUpdateApplier> log)
    {
        _log = log;
    }

    /// <summary>Consulta periódica (cada 15 minutos) para equipos sin aviso en tiempo real.</summary>
    public async Task CheckAsync(AgentOptions options, CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastCheck < TimeSpan.FromMinutes(15)) return;
        _lastCheck = DateTime.UtcNow;
        await CheckNowAsync(options, ct);
    }

    public async Task CheckNowAsync(AgentOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.ServerUrl)) return;

        try
        {
            var manifest = await Http.GetFromJsonAsync<ClientUpdateManifestDto>(
                $"{options.ServerUrl.TrimEnd('/')}/api/client-update/manifest", ct);
            if (manifest is not null) await ApplyAsync(options, manifest, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Actualización del cliente: no se pudo consultar el manifiesto");
        }
    }

    public async Task ApplyAsync(AgentOptions options, ClientUpdateManifestDto manifest, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version)) return;
        if (string.Equals(_lastAppliedVersion, manifest.Version, StringComparison.OrdinalIgnoreCase)) return;
        if (!VersionUtil.IsNewer(manifest.Version, AppInfo.Version)) return;

        var clientDir = ClientUpdateInstaller.ResolveClientDir();
        if (clientDir is null)
        {
            _log.LogDebug("Actualización del cliente: no se encontró la carpeta Client junto al agente");
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            var workDir = Path.Combine(Path.GetTempPath(), "CyberManager", "AgentClientUpdate",
                manifest.Version);
            var staging = await ClientUpdateInstaller.DownloadAndExtractAsync(
                options.ServerUrl, manifest, workDir, Http, ct);

            if (staging is null)
            {
                _log.LogWarning("Actualización del cliente: el SHA-256 del paquete no coincide");
                return;
            }

            if (!ClientUpdateInstaller.Apply(staging, clientDir, out var error))
            {
                _log.LogWarning("Actualización del cliente: {Error}", error);
                return;
            }

            _lastAppliedVersion = manifest.Version;
            _log.LogInformation("Cliente actualizado a {Version}; la estación se reiniciará",
                manifest.Version);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Actualización del cliente fallida");
        }
        finally
        {
            _gate.Release();
        }
    }
}
