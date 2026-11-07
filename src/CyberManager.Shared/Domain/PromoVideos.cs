namespace CyberManager.Shared.Domain;

/// <summary>Video de promoción que se reproduce en los equipos bloqueados.</summary>
public class PromoVideo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = "video/mp4";
    public string Extension { get; set; } = ".mp4";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    /// <summary>Incluido en la lista que reciben los terminales.</summary>
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
