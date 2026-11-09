namespace CyberManager.Shared;

public enum UserRole
{
    Admin = 0,
    Cashier = 1,
    Technician = 2
}

public enum StationStatus
{
    Offline = 0,
    Available = 1,
    InUse = 2,
    Paused = 3,
    Maintenance = 4,
    Reserved = 5
}

/// <summary>Tipo de equipo atendido por el local.</summary>
public enum StationKind
{
    /// <summary>Computadora personal.</summary>
    Pc = 0,
    /// <summary>Consola de videojuegos.</summary>
    Console = 1,
    /// <summary>Otro equipo (TV, sala, etc.).</summary>
    Other = 2
}

public enum SessionType
{
    Postpaid = 0,
    Prepaid = 1,
    Free = 2
}

public enum SessionStatus
{
    Active = 0,
    Paused = 1,
    Closed = 2,
    Cancelled = 3
}

public enum PaymentMethod
{
    /// <summary>Efectivo en bolívares.</summary>
    Cash = 0,
    /// <summary>Punto de venta / tarjeta en bolívares.</summary>
    Card = 1,
    Transfer = 2,
    CustomerBalance = 3,
    Mixed = 4,
    /// <summary>Efectivo en dólares (aplica IGTF si está habilitado).</summary>
    CashUsd = 5,
    /// <summary>Cargado a la cuenta de la estación; se cobra al cerrar la sesión.</summary>
    Pending = 6,
    /// <summary>Pago Móvil venezolano (cobro electrónico en bolívares).</summary>
    PagoMovil = 7
}

public enum Currency
{
    /// <summary>Dólar estadounidense (moneda base del sistema).</summary>
    Usd = 0,
    /// <summary>Bolívar (moneda de pago).</summary>
    Ves = 1
}

public enum RateSource
{
    Manual = 0,
    Bcv = 1,
    Paralelo = 2
}

public enum InvoiceType
{
    Ticket = 0,
    Factura = 1,
    NotaEntrega = 2
}

public enum StockMovementType
{
    Initial = 0,
    Purchase = 1,
    Sale = 2,
    Adjustment = 3,
    Waste = 4,
    Return = 5
}

public enum CashMovementType
{
    Opening = 0,
    Sale = 1,
    Deposit = 2,
    Withdrawal = 3,
    Expense = 4,
    Refund = 5,
    TimeSale = 6
}

public enum SaleStatus
{
    Completed = 0,
    Cancelled = 1
}

public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5
}

public enum CustomerTransactionType
{
    Recharge = 0,
    Consumption = 1,
    Refund = 2,
    Adjustment = 3,
    Credit = 4
}

public enum AgentCommandType
{
    Lock = 0,
    Unlock = 1,
    Shutdown = 2,
    Restart = 3,
    Logoff = 4,
    Message = 5,
    KillProcess = 6,
    Screenshot = 7,
    SetKiosk = 8,
    SyncState = 9,
    /// <summary>Ajusta el volumen maestro del equipo. Argumento: porcentaje 0-100.</summary>
    SetVolume = 10,
    /// <summary>Silencia el audio del equipo.</summary>
    Mute = 11,
    /// <summary>Reactiva el audio del equipo.</summary>
    Unmute = 12,
    /// <summary>Bloquea teclado y mouse hasta recibir <see cref="UnlockInput"/>.</summary>
    LockInput = 13,
    UnlockInput = 14,
    /// <summary>Apaga el monitor; se enciende con <see cref="MonitorOn"/> o con cualquier tecla.</summary>
    MonitorOff = 15,
    MonitorOn = 16
}

/// <summary>Tipo de regla del módulo NetFiltroWeb.</summary>
public enum WebFilterRuleKind
{
    /// <summary>Sitio o dominio exacto (bloquea también sus subdominios).</summary>
    Domain = 0,
    /// <summary>Palabra clave que se busca en el nombre del sitio.</summary>
    Keyword = 1
}

/// <summary>Cómo se muestran los fondos de pantalla en los equipos bloqueados.</summary>
public enum WallpaperMode
{
    /// <summary>Sin fondo: se usa el degradado del cliente.</summary>
    None = 0,
    /// <summary>Un único fondo fijo.</summary>
    Single = 1,
    /// <summary>Presentación rotativa con todos los fondos activos.</summary>
    Slideshow = 2
}

public enum StationEventType
{
    Started = 0,
    Paused = 1,
    Resumed = 2,
    Ended = 3,
    Extended = 4,
    Warning = 5,
    Locked = 6,
    Unlocked = 7,
    Offline = 8,
    Online = 9
}
