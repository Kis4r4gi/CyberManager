namespace CyberManager.Shared.Dtos;

/// <summary>
/// Estado de la última versión publicada en GitHub comparada con la versión instalada.
/// </summary>
public sealed record UpdateInfoDto(
    string CurrentVersion,
    string? LatestVersion,
    bool IsNewer,
    bool ReminderEnabled,
    int DelayHours,
    DateTime? PublishedUtc,
    DateTime? ReminderAtUtc,
    bool IsReminderDue,
    bool IsDismissed,
    string? ReleaseUrl,
    string? ReleaseName,
    string? ReleaseNotes,
    DateTime CheckedUtc,
    bool CheckFailed,
    string? Error);

/// <summary>Oculta el recordatorio de una versión concreta hasta que se publique otra.</summary>
public sealed record DismissUpdateRequest(string Version);
