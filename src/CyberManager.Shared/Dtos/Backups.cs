namespace CyberManager.Shared.Dtos;

/// <summary>Archivo de respaldo de la base de datos disponible en el servidor.</summary>
public record BackupFileDto(
    string FileName,
    long SizeBytes,
    DateTime CreatedUtc,
    bool Automatic);

/// <summary>Estado del respaldo automático y archivos disponibles.</summary>
public record BackupListDto(
    string Folder,
    bool AutoBackupEnabled,
    int IntervalHours,
    int Retention,
    DateTime? LastBackupUtc,
    List<BackupFileDto> Files,
    /// <summary>Carpeta predeterminada junto a la base de datos, para poder volver a ella.</summary>
    string? DefaultFolder = null);

/// <summary>Cambia la carpeta de respaldos. Null o vacío restablece la predeterminada.</summary>
public record SetBackupFolderRequest(string? Folder);

/// <summary>Resultado de restaurar una base de datos.</summary>
public record RestoreBackupResultDto(
    string RestoredFile,
    string SafetyBackupFile,
    DateTime RestoredUtc);
