using System.Collections.Concurrent;
using CyberManager.Server.Data;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>Vigila equipos desconectados, avisa cuando se acaba el tiempo y bloquea en overtime.</summary>
public class StationMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<StationMonitorService> _log;
    private readonly ConcurrentDictionary<int, int> _lastWarned = new();

    public StationMonitorService(IServiceScopeFactory scopes, ILogger<StationMonitorService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error en el monitor de estaciones");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rt = scope.ServiceProvider.GetRequiredService<RealtimeService>();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();

        var now = DateTime.UtcNow;
        var offlineThreshold = TimeSpan.FromSeconds(75);

        var stations = await db.Stations.ToListAsync(ct);
        foreach (var st in stations)
        {
            var stale = st.LastSeenUtc is null || now - st.LastSeenUtc > offlineThreshold;
            if (!stale) continue;
            if (st.Status is StationStatus.Maintenance or StationStatus.Offline) continue;

            if (st.CurrentSessionId is null)
            {
                st.Status = StationStatus.Offline;
                await rt.BroadcastEventAsync("StationOffline", $"{st.Name} sin conexión");
            }
        }

        var warningMinutes = await settings.GetIntAsync(SettingsKeys.WarningMinutes, 5);
        var autoLock = await settings.GetBoolAsync(SettingsKeys.AutoLockOvertime, true);

        var sessions = await db.Sessions.Include(s => s.Station)
            .Where(s => s.Status != SessionStatus.Closed && s.Status != SessionStatus.Cancelled)
            .ToListAsync(ct);

        foreach (var s in sessions)
        {
            if (s.Type != SessionType.Prepaid) continue;

            var elapsed = SessionCalculator.ElapsedMinutes(s, now);
            var remaining = s.PlannedMinutes - elapsed;

            if (remaining <= warningMinutes && remaining > 0)
            {
                if (_lastWarned.TryGetValue(s.Id, out var last) && last == remaining) continue;
                _lastWarned[s.Id] = remaining;
                await rt.SessionWarningAsync(new SessionWarningEvent(s.Id, s.StationId,
                    s.Station?.Name ?? $"PC {s.StationId}", remaining, false));
            }
            else if (remaining <= 0)
            {
                if (!_lastWarned.ContainsKey(s.Id) || _lastWarned[s.Id] > 0)
                {
                    _lastWarned[s.Id] = remaining;
                    await rt.SessionWarningAsync(new SessionWarningEvent(s.Id, s.StationId,
                        s.Station?.Name ?? $"PC {s.StationId}", remaining, true));
                }

                if (autoLock && s.Station is not null && !s.Station.IsLocked)
                {
                    s.Station.IsLocked = true;
                    await rt.SendAgentCommandAsync(s.StationId, AgentCommandType.Lock);
                    await rt.SendStationCommandAsync(s.StationId, AgentCommandType.Lock);
                    await rt.BroadcastEventAsync("Overtime", $"{s.Station.Name}: tiempo agotado, equipo bloqueado");
                }
            }
            else
            {
                _lastWarned.TryRemove(s.Id, out _);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
