namespace CyberManager.Shared.Domain;

/// <summary>Zona o área del local (VIP, estándar, consolas, etc.).</summary>
public class Zone
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563EB";
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
    public List<Station> Stations { get; set; } = new();
}

/// <summary>Tarifa por hora aplicable a una zona o equipo.</summary>
public class Rate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PricePerHour { get; set; }
    public int MinimumMinutes { get; set; } = 1;
    /// <summary>Fracción de cobro en minutos (0 = cobro por minuto exacto).</summary>
    public int BillingBlockMinutes { get; set; } = 1;
    public bool Active { get; set; } = true;
    public List<Station> Stations { get; set; } = new();
}

/// <summary>Paquete prepago de tiempo (ej. 60 min + 10 de bono).</summary>
public class TimePackage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Minutes { get; set; }
    public int BonusMinutes { get; set; }
    public decimal Price { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>Equipo de la red (PC o consola).</summary>
public class Station
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public int? ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public int? RateId { get; set; }
    public Rate? Rate { get; set; }
    /// <summary>PC, consola de videojuegos u otro tipo de puesto.</summary>
    public StationKind Kind { get; set; } = StationKind.Pc;
    /// <summary>Plataforma o modelo del equipo (ej. PlayStation 5, Xbox Series X).</summary>
    public string? Platform { get; set; }
    /// <summary>Cantidad de mandos/controles disponibles para consolas.</summary>
    public int? Controllers { get; set; }
    public StationStatus Status { get; set; } = StationStatus.Offline;
    public int? CurrentSessionId { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
    public string? DeviceToken { get; set; }
    public string? Version { get; set; }
    public string? CurrentUser { get; set; }
    public bool IsLocked { get; set; } = true;
    public List<Session> Sessions { get; set; } = new();
}

/// <summary>Sesión de uso de un equipo (tiempo vendido o por cobrar).</summary>
public class Session
{
    public int Id { get; set; }
    public int StationId { get; set; }
    public Station? Station { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public SessionType Type { get; set; } = SessionType.Postpaid;
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedUtc { get; set; }
    public DateTime? PausedAtUtc { get; set; }
    public int PausedSeconds { get; set; }
    /// <summary>Minutos comprados en prepago. 0 significa tiempo libre (postpago).</summary>
    public int PlannedMinutes { get; set; }
    public decimal RatePerHour { get; set; }
    public int MinimumMinutes { get; set; } = 1;
    public int BillingBlockMinutes { get; set; } = 1;
    /// <summary>Importe de tiempo ya cobrado (prepago o al cerrar).</summary>
    public decimal AmountCharged { get; set; }
    public int? OpenedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public string? Notes { get; set; }
    public int? CashSessionId { get; set; }
    public List<Sale> Sales { get; set; } = new();
}

/// <summary>Reserva de un equipo para una fecha/hora.</summary>
public class Reservation
{
    public int Id { get; set; }
    public int StationId { get; set; }
    public Station? Station { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
