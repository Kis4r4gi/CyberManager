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

/// <summary>Configuración de fondos que el servidor entrega a cada estación.</summary>
public record WallpaperPlaylistDto(
    int RotationSeconds, WallpaperOrder Order, WallpaperTransition Transition,
    List<WallpaperItemDto> Items, string Revision);

/// <summary>Configuración de fondos que se guarda desde la consola de administración.</summary>
public record ApplyWallpaperSettingsRequest(
    int RotationSeconds, WallpaperOrder Order, WallpaperTransition Transition);

/// <summary>Claves de configuración compartidas por el servidor y la consola.</summary>
public static class WallpaperSettings
{
    public const string RotationSeconds = "WallpaperRotationSeconds";
    public const string Order = "WallpaperOrder";
    public const string Transition = "WallpaperTransition";

    public const int DefaultRotationSeconds = 30;
    public const int MinRotationSeconds = 5;
    public const int MaxRotationSeconds = 3600;

    public static WallpaperOrder ParseOrder(string? value)
        => Enum.TryParse<WallpaperOrder>(value, ignoreCase: true, out var order)
            ? order
            : WallpaperOrder.Ordered;

    public static WallpaperTransition ParseTransition(string? value)
        => Enum.TryParse<WallpaperTransition>(value, ignoreCase: true, out var transition)
            ? transition
            : WallpaperTransition.Fade;

    public static string OrderText(WallpaperOrder order) => order switch
    {
        WallpaperOrder.Random => "Aleatorio",
        _ => "Ordenado"
    };

    public static string TransitionText(WallpaperTransition transition) => transition switch
    {
        WallpaperTransition.None => "Sin transición",
        WallpaperTransition.Slide => "Deslizamiento",
        WallpaperTransition.Zoom => "Zoom",
        _ => "Fundido"
    };

    public static string Invariant(int value)
        => value.ToString(CultureInfo.InvariantCulture);
}
