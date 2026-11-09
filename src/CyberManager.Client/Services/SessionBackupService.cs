using System.IO;
using System.Text.Json;
using CyberManager.Desktop.Api;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client.Services;

/// <summary>
/// Guarda una copia local del estado de la sesión para que el tiempo del cliente
/// no se pierda si se corta la energía o el servidor deja de responder.
/// El archivo se escribe de forma atómica (temporal + reemplazo) para no quedar
/// corrupto si el equipo se apaga durante la escritura.
/// </summary>
public static class SessionBackupService
{
    private sealed record Snapshot(int StationId, DateTime SavedUtc, StationClientStateDto State);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static string BackupFile =>
        Path.Combine(AppPaths.DataDir("Client"), "session-backup.json");

    /// <summary>Guarda el estado actual. Si no hay sesión activa, borra el respaldo.</summary>
    public static void Save(StationClientStateDto state)
    {
        try
        {
            if (state.Session is null)
            {
                Clear();
                return;
            }

            var snapshot = new Snapshot(state.StationId, DateTime.UtcNow, state);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var file = BackupFile;
            var temp = file + ".tmp";

            File.WriteAllText(temp, json);
            File.Move(temp, file, overwrite: true);
        }
        catch
        {
            // Un fallo de disco no debe interrumpir la sesión del cliente.
        }
    }

    /// <summary>
    /// Recupera el respaldo y avanza el tiempo transcurrido desde que se guardó.
    /// Devuelve null si no hay respaldo o pertenece a otro equipo.
    /// </summary>
    public static StationClientStateDto? TryRestore(int stationId)
    {
        try
        {
            var file = BackupFile;
            if (!File.Exists(file)) return null;

            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file), JsonOptions);
            if (snapshot?.State is null || snapshot.StationId != stationId) return null;

            var session = snapshot.State.Session;
            if (session is null) return null;

            var deltaMinutes = (int)Math.Max(0, (DateTime.UtcNow - snapshot.SavedUtc).TotalMinutes);
            if (deltaMinutes == 0)
                return snapshot.State with { ServerTimeUtc = DateTime.UtcNow };

            var extraAmount = session.Type == SessionType.Free
                ? 0
                : Math.Round(deltaMinutes / 60m * session.RatePerHour, 2);
            var timeAmount = Math.Round(session.TimeAmount + extraAmount, 2);
            var remaining = session.RemainingMinutes - deltaMinutes;

            var restored = session with
            {
                ElapsedMinutes = session.ElapsedMinutes + deltaMinutes,
                RemainingMinutes = Math.Max(0, remaining),
                TimeAmount = timeAmount,
                TotalAmount = Math.Round(timeAmount + session.ConsumptionAmount, 2),
                Overtime = session.Type == SessionType.Prepaid
                    ? remaining < 0
                    : session.Overtime || remaining < 0
            };

            return snapshot.State with
            {
                Session = restored,
                ServerTimeUtc = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        try
        {
            var file = BackupFile;
            if (File.Exists(file)) File.Delete(file);
        }
        catch
        {
            // Sin acción: el próximo guardado sobrescribe el archivo.
        }
    }
}
