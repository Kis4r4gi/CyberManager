using System.Globalization;

namespace CyberManager.Shared.Dtos;

public record WallpaperDto(
    int Id, string Name, string ContentType, string Extension, long SizeBytes,
    int Width, int Height, string Sha256, bool Active, int SortOrder,
    DateTime CreatedUtc, DateTime UpdatedUtc);

public record SaveWallpaperRequest(string Name, bool Active, int SortOrder);

/// <summary>Datos mínimos que necesita el cliente para descargar y cachear un fondo.</summary>
public record WallpaperItemDto(
    int Id, string Name, string ContentType, string Extension, string Sha256,
    int Width, int Height, long SizeBytes, DateTime UpdatedUtc);

/// <summary>Lista reproducible que el servidor envía a las estaciones.</summary>
public record WallpaperPlaylistDto(
    WallpaperMode Mode, int RotationSeconds, int? ActiveId,
    List<WallpaperItemDto> Items, string Revision);

public record ApplyWallpaperSettingsRequest(
    WallpaperMode Mode, int? ActiveId, int RotationSeconds);

/// <summary>Claves de configuración compartidas por el servidor y la consola.</summary>
public static class WallpaperSettings
{
    public const string Mode = "WallpaperMode";
    public const string RotationSeconds = "WallpaperRotationSeconds";
    public const string ActiveId = "WallpaperActiveId";

    public const int DefaultRotationSeconds = 30;
    public const int MinRotationSeconds = 5;
    public const int MaxRotationSeconds = 3600;

    public static WallpaperMode ParseMode(string? value)
        => Enum.TryParse<WallpaperMode>(value, ignoreCase: true, out var mode)
            ? mode
            : WallpaperMode.None;

    public static string ModeText(WallpaperMode mode) => mode switch
    {
        WallpaperMode.Single => "Fondo fijo",
        WallpaperMode.Slideshow => "Presentación rotativa",
        _ => "Sin fondos"
    };

    public static string Invariant(int value)
        => value.ToString(CultureInfo.InvariantCulture);
}
