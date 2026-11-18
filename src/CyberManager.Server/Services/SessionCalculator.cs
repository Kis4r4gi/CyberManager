using CyberManager.Shared;
using CyberManager.Shared.Domain;

namespace CyberManager.Server.Services;

public static class SessionCalculator
{
    /// <summary>Segundos efectivos transcurridos descontando pausas.</summary>
    public static double ElapsedSeconds(Session s, DateTime nowUtc)
    {
        var end = s.Status == SessionStatus.Paused && s.PausedAtUtc.HasValue
            ? s.PausedAtUtc.Value
            : s.EndedUtc ?? nowUtc;

        var seconds = (end - s.StartedUtc).TotalSeconds - s.PausedSeconds;
        return seconds < 0 ? 0 : seconds;
    }

    /// <summary>Minutos efectivos transcurridos descontando pausas.</summary>
    public static int ElapsedMinutes(Session s, DateTime nowUtc)
        => (int)Math.Floor(ElapsedSeconds(s, nowUtc) / 60.0);

    /// <summary>
    /// Minutos consumidos redondeando hacia arriba. Se usa al cerrar el prepago:
    /// una sesión de 40 segundos consume 1 minuto y no permite devolver el paquete
    /// completo (evita tiempo gratis repitiendo sesiones cortas).
    /// </summary>
    public static int BilledMinutes(Session s, DateTime nowUtc)
    {
        var seconds = ElapsedSeconds(s, nowUtc);
        return seconds <= 0 ? 0 : (int)Math.Ceiling(seconds / 60.0);
    }

    /// <summary>Importe del tiempo consumido. En prepago devuelve lo ya cobrado.</summary>
    public static decimal TimeAmount(Session s, int elapsedMinutes)
    {
        if (s.Type == SessionType.Free) return 0m;
        if (s.Type == SessionType.Prepaid) return s.AmountCharged;

        var minutes = Math.Max(elapsedMinutes, s.MinimumMinutes);
        if (s.BillingBlockMinutes > 1)
            minutes = (int)(Math.Ceiling(minutes / (double)s.BillingBlockMinutes) * s.BillingBlockMinutes);

        return Math.Round(s.RatePerHour * minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    public static int RemainingMinutes(Session s, int elapsedMinutes)
        => s.Type == SessionType.Prepaid ? s.PlannedMinutes - elapsedMinutes : 0;

    public static bool IsOvertime(Session s, int elapsedMinutes)
        => s.Type == SessionType.Prepaid && elapsedMinutes > s.PlannedMinutes;
}
