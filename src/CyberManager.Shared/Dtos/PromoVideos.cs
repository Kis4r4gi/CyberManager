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
    public const long MaxBytes = 300L * 1024 * 1024;
    public static readonly string[] Extensions = { ".mp4", ".m4v", ".wmv", ".avi" };

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
