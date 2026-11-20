namespace CyberManager.Shared.Dtos;

public record PromoVideoDto(
    int Id, string Name, string ContentType, string Extension, long SizeBytes,
    string Sha256, bool Active, int SortOrder, DateTime CreatedUtc, DateTime UpdatedUtc);

public record SavePromoVideoRequest(string Name, bool Active, int SortOrder);

/// <summary>Datos mínimos que necesita el terminal para descargar y reproducir un video.</summary>
public record PromoVideoItemDto(
    int Id, string Name, string ContentType, string Extension, string Sha256,
    long SizeBytes, DateTime UpdatedUtc);

/// <summary>Lista de videos activos que el servidor sincroniza con los terminales.</summary>
public record PromoVideoPlaylistDto(List<PromoVideoItemDto> Items, string Revision);

public static class PromoSettings
{
    /// <summary>Límite por video. Se envía y descarga en bloques para no cargarlo en memoria.</summary>
    public const long MaxBytes = 120L * 1024 * 1024;
    /// <summary>Solo MP4/M4V: el reproductor del cliente no acepta otros contenedores.</summary>
    public static readonly string[] Extensions = { ".mp4", ".m4v" };

    public static string ContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".wmv" => "video/x-ms-wmv",
        ".avi" => "video/x-msvideo",
        _ => "application/octet-stream"
    };

    public static bool IsSupported(string fileName)
        => Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}
