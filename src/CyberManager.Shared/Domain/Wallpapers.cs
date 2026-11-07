namespace CyberManager.Shared.Domain;

/// <summary>Imagen de la galería que se muestra en la pantalla de bloqueo de los equipos.</summary>
public class Wallpaper
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/png";
    public string Extension { get; set; } = ".png";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    /// <summary>Incluido en la presentación rotativa o disponible para fondo fijo.</summary>
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
