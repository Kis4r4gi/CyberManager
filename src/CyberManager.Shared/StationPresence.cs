namespace CyberManager.Shared;

/// <summary>
/// Determina si un equipo está en línea a partir de su último latido. El estado
/// de la estación (disponible, en uso, en pausa, etc.) describe su ocupación;
/// la presencia se calcula por separado para no confundir un equipo apagado con
/// uno que tiene una sesión abierta.
/// </summary>
public static class StationPresence
{
    /// <summary>Tiempo máximo sin latido antes de considerar el equipo desconectado.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    public static bool IsOnline(DateTime? lastSeenUtc, DateTime nowUtc)
    {
        if (lastSeenUtc is not { } seen) return false;

        // SQLite devuelve las fechas sin zona horaria; el valor guardado es UTC.
        if (seen.Kind == DateTimeKind.Local) seen = seen.ToUniversalTime();
        else if (seen.Kind == DateTimeKind.Unspecified) seen = DateTime.SpecifyKind(seen, DateTimeKind.Utc);

        var elapsed = nowUtc - seen;
        // Se tolera un pequeño desfase de reloj entre el servidor y la consola.
        return elapsed <= Timeout && elapsed > TimeSpan.FromMinutes(-5);
    }

    public static bool IsOnline(DateTime? lastSeenUtc) => IsOnline(lastSeenUtc, DateTime.UtcNow);
}
