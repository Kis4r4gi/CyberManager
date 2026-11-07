using CyberManager.Shared;
using CyberManager.Shared.Domain;

namespace CyberManager.Server.Services;

public static class SessionCalculator
{
    /// <summary>Minutos efectivos transcurridos descontando pausas.</summary>
    public static int ElapsedMinutes(Session s, DateTime nowUtc)
    {
        var end = s.Status == SessionStatus.Paused && s.PausedAtUtc.HasValue
            ? s.PausedAtUtc.Value
            : s.EndedUtc ?? nowUtc;

        var seconds = (end - s.StartedUtc).TotalSeconds - s.PausedSeconds;
        if (seconds < 0) seconds = 0;
        return (int)Math.Floor(seconds / 60.0);
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
