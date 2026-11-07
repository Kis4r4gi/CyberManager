namespace CyberManager.Shared.Domain;

/// <summary>
/// Tasa de cambio diaria bolívar / dólar. Se guarda una por día y fuente.
/// Todos los importes del sistema se manejan en USD (moneda base) y se
/// convierten a Bs con la tasa vigente del día.
/// </summary>
public class ExchangeRate
{
    public int Id { get; set; }
    /// <summary>Día al que aplica la tasa (UTC, hora 00:00).</summary>
    public DateTime Date { get; set; }
    public decimal Rate { get; set; }
    public RateSource Source { get; set; } = RateSource.Manual;
    public bool IsActive { get; set; } = true;
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

/// <summary>Secuencia de números de control para facturas (formato SENIAT).</summary>
public class InvoiceSequence
{
    public int Id { get; set; }
    public InvoiceType Type { get; set; }
    public string Prefix { get; set; } = "00";
    public long NextNumber { get; set; } = 1;
    public long LastNumber { get; set; } = 99999999;
    public bool Active { get; set; } = true;
}
