using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Services;

/// <summary>
/// Busca estaciones en la red aunque nadie abra la consola. Solo da de alta equipos
/// que reportan MAC y reenvía la configuración para reparar tokens desactualizados.
/// </summary>
public sealed class StationAutoDiscoveryWorker(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<StationAutoDiscoveryWorker> log) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "No se pudo completar el descubrimiento automático de estaciones.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
        if (!await settings.GetBoolAsync(SettingsKeys.ScannerAutoAdd, true)) return;

        var discovery = scope.ServiceProvider.GetRequiredService<StationDiscoveryService>();
        var found = await discovery.ScanAsync(ct);
        if (found.Count == 0) return;

        var items = found.Select(m => new DiscoveryApplyItem(
            m.Role, m.StationId, m.Token, m.MachineName, m.IpAddress, m.MacAddress, m.Version))
            .ToList();

        var port = configuration.GetValue<int>("Server:Port", 5080);
        var ip = NetworkIdentity.LocalIPv4Addresses(onlyWithGateway: true).FirstOrDefault()
                 ?? NetworkIdentity.LocalIPv4() ?? "127.0.0.1";
        var serverUrl = $"http://{ip}:{port}";

        var result = await discovery.ApplyAsync(items, serverUrl, automatic: true, ct);
        if (result.Created > 0 || result.Updated > 0 || result.Messages.Count > 0)
        {
            log.LogInformation(
                "Descubrimiento automático: {Found} equipos en la red, {Created} nuevos, {Updated} actualizados, {Configured} configurados.",
                found.Count, result.Created, result.Updated, result.Configured);
            foreach (var message in result.Messages)
                log.LogInformation("Descubrimiento: {Message}", message);
        }
    }
}
