namespace CyberManager.Shared.Dtos;

/// <summary>Latido que envía el agente de cada equipo.</summary>
public record AgentHeartbeat(
    int StationId, string MachineName, string? CurrentUser, string Version,
    string OsVersion, double CpuPercent, double RamPercent, string AgentState);

public record AgentCommand(Guid CommandId, AgentCommandType Command, string? Argument, int? DurationSeconds);

public record CommandResult(Guid CommandId, bool Success, string? Message);

/// <summary>Estado que el servidor empuja a las pantallas de administración.</summary>
public record RealtimeEvent(string Type, string? Message, object? Payload);

public record StationStateEvent(int StationId, StationStatus Status, int? SessionId,
    int ElapsedMinutes, int RemainingMinutes, decimal Amount, bool Overtime);

public record SessionWarningEvent(int SessionId, int StationId, string StationName, int RemainingMinutes, bool Overtime);

/// <summary>Solicitud de captura de pantalla que el servidor envía a la estación.</summary>
public record ScreenFrameRequest(int StationId, Guid RequestId, int MaxWidth, int Quality);

/// <summary>Captura JPEG que devuelve la estación para el visualizador remoto.</summary>
public record ScreenFrameResult(int StationId, Guid RequestId, string? ImageBase64,
    int Width, int Height, DateTime CapturedAtUtc, string? Error);
