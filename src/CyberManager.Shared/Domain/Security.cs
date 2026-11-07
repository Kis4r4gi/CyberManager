namespace CyberManager.Shared.Domain;

/// <summary>Usuario del sistema (administrador, cajero o técnico).</summary>
public class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool Active { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }
}

/// <summary>Parámetros generales del negocio guardados como clave/valor.</summary>
public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Registro de un equipo (agente) autorizado a conectarse.</summary>
public class DeviceRegistration
{
    public int Id { get; set; }
    public int? StationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? MachineName { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public bool Active { get; set; } = true;
}
