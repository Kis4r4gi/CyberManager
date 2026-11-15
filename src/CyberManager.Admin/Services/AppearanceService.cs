using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CyberManager.Desktop.Api;
using CyberManager.Shared;

namespace CyberManager.Admin.Services;

/// <summary>Colores e iconos de la consola, guardados localmente por equipo.</summary>
public sealed class AppearanceSettings
{
    public string Preset { get; set; } = "Cyber";
    public string Background { get; set; } = "#070B14";
    public string Surface { get; set; } = "#0F172A";
    public string SurfaceAlt { get; set; } = "#16233A";
    public string Border { get; set; } = "#33517A";
    public string Text { get; set; } = "#E8EFFA";
    public string Muted { get; set; } = "#8FA4C4";
    public string Accent { get; set; } = "#22D3EE";
    public string Accent2 { get; set; } = "#3B82F6";
    public string Success { get; set; } = "#22C55E";
    public string Warning { get; set; } = "#F59E0B";
    public string Danger { get; set; } = "#EF4444";
    public string Purple { get; set; } = "#8B5CF6";
    /// <summary>Tamaño de los iconos del menú lateral (12 a 26 px).</summary>
    public double NavIconSize { get; set; } = 16;
    public double NavFontSize { get; set; } = 13;
    /// <summary>Escala de los cuadros de artículos del punto de venta (80 a 160 %).</summary>
    public double ProductCardScale { get; set; } = 100;
    /// <summary>Escala de los cuadros de terminales del tablero (80 a 160 %).</summary>
    public double StationCardScale { get; set; } = 100;
    /// <summary>Idioma de la consola: "es" o "en".</summary>
    public string Language { get; set; } = "es";

    // ---------------- Personalización del tablero de equipos ----------------
    /// <summary>Orden del tablero: manual, name, status, zone, kind, time o amount.</summary>
    public string DashboardSort { get; set; } = "manual";
    public bool DashboardSortDescending { get; set; }
    public bool DashboardShowMetrics { get; set; } = true;
    public bool DashboardShowLegend { get; set; } = true;
    /// <summary>Mostrar solo equipos con sesión activa o en pausa.</summary>
    public bool DashboardOnlyInUse { get; set; }
    /// <summary>Zona seleccionada; null o 0 = todas las zonas.</summary>
    public int? DashboardZoneId { get; set; }
    /// <summary>Texto del buscador del tablero.</summary>
    public string DashboardSearch { get; set; } = string.Empty;

    public AppearanceSettings Clone() => (AppearanceSettings)MemberwiseClone();

    public static AppearanceSettings ForPreset(string preset) => preset switch
    {
        "Neón" => new AppearanceSettings
        {
            Preset = preset,
            Background = "#0A0614", Surface = "#160F26", SurfaceAlt = "#221737",
            Border = "#4A376B", Text = "#F4ECFF", Muted = "#B9A6D6",
            Accent = "#F472B6", Accent2 = "#A855F7", Success = "#34D399",
            Warning = "#FBBF24", Danger = "#FB7185", Purple = "#C084FC"
        },
        "Esmeralda" => new AppearanceSettings
        {
            Preset = preset,
            Background = "#04110D", Surface = "#0A1F18", SurfaceAlt = "#103026",
            Border = "#286B54", Text = "#E8FFF6", Muted = "#8FC4AE",
            Accent = "#34D399", Accent2 = "#0EA5E9", Success = "#22C55E",
            Warning = "#F59E0B", Danger = "#EF4444", Purple = "#8B5CF6"
        },
        "Ámbar" => new AppearanceSettings
        {
            Preset = preset,
            Background = "#120C04", Surface = "#211707", SurfaceAlt = "#34240C",
            Border = "#7A5A26", Text = "#FFF5E1", Muted = "#CFB183",
            Accent = "#FBBF24", Accent2 = "#F97316", Success = "#84CC16",
            Warning = "#F59E0B", Danger = "#EF4444", Purple = "#A78BFA"
        },
        "Océano" => new AppearanceSettings
        {
            Preset = preset,
            Background = "#04101F", Surface = "#0A1D33", SurfaceAlt = "#102B49",
            Border = "#2B5B87", Text = "#EAF4FF", Muted = "#93B4D4",
            Accent = "#38BDF8", Accent2 = "#6366F1", Success = "#2DD4BF",
            Warning = "#FBBF24", Danger = "#F87171", Purple = "#A78BFA"
        },
        "Grafito" => new AppearanceSettings
        {
            Preset = preset,
            Background = "#0B0D10", Surface = "#161A20", SurfaceAlt = "#222832",
            Border = "#4A5666", Text = "#F1F4F8", Muted = "#A3AEBD",
            Accent = "#E2E8F0", Accent2 = "#60A5FA", Success = "#4ADE80",
            Warning = "#FACC15", Danger = "#F87171", Purple = "#C4B5FD"
        },
        _ => new AppearanceSettings()
    };

    public static IReadOnlyList<string> Presets { get; } = new[] { "Cyber", "Neón", "Esmeralda", "Ámbar", "Océano", "Grafito" };
}

public static class AppearanceService
{
    public static AppearanceSettings Current { get; private set; } = new();
    public static event Action<AppearanceSettings>? Changed;

    private static string FilePath => Path.Combine(AppPaths.DataDir("Admin"), "appearance.json");

    public static void Initialize()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppearanceSettings>(File.ReadAllText(FilePath));
                if (loaded is not null)
                {
                    // Migra el borde oscuro original a un tono más legible en pantalla.
                    if (string.Equals(loaded.Border, "#24344F", StringComparison.OrdinalIgnoreCase))
                    {
                        loaded.Border = "#33517A";
                        Current = loaded;
                        Apply(Current, save: true);
                        return;
                    }

                    Current = loaded;
                }
            }
        }
        catch
        {
            Current = new AppearanceSettings();
        }

        Apply(Current, save: false);
    }

    /// <summary>Aplica los colores a los pinceles existentes (StaticResource) y notifica a las ventanas.</summary>
    public static void Apply(AppearanceSettings settings, bool save)
    {
        Current = settings.Clone();
        Current.NavIconSize = Math.Clamp(settings.NavIconSize, 12, 26);
        Current.NavFontSize = Math.Clamp(settings.NavFontSize, 11, 17);
        Current.ProductCardScale = Math.Clamp(settings.ProductCardScale, 80, 160);
        Current.StationCardScale = Math.Clamp(settings.StationCardScale, 80, 160);
        Current.Language = L.Parse(settings.Language) == AppLanguage.English ? "en" : "es";
        Current.DashboardSort = NormalizeDashboardSort(settings.DashboardSort);
        Current.DashboardZoneId = settings.DashboardZoneId is > 0 ? settings.DashboardZoneId : null;
        Current.DashboardSearch = (settings.DashboardSearch ?? string.Empty).Trim();

        // El idioma se aplica antes de repintar para que las vistas se reconstruyan traducidas.
        L.SetLanguage(Current.Language);

        var bg = Parse(Current.Background, Colors.Black);
        var surface = Parse(Current.Surface, Colors.Black);
        var surfaceAlt = Parse(Current.SurfaceAlt, Colors.Black);

        Set("BgBrush", bg);
        Set("SurfaceBrush", surface);
        Set("SurfaceAltBrush", surfaceAlt);
        Set("BorderBrush", Parse(Current.Border, Colors.Gray));
        Set("TextBrush", Parse(Current.Text, Colors.White));
        Set("MutedBrush", Parse(Current.Muted, Colors.Gray));
        Set("AccentBrush", Parse(Current.Accent, Colors.Cyan));
        Set("Accent2Brush", Parse(Current.Accent2, Colors.Blue));
        Set("SuccessBrush", Parse(Current.Success, Colors.Green));
        Set("WarningBrush", Parse(Current.Warning, Colors.Orange));
        Set("DangerBrush", Parse(Current.Danger, Colors.Red));
        Set("PurpleBrush", Parse(Current.Purple, Colors.Purple));

        // Superficies derivadas para que la ventana acompañe la paleta elegida.
        Set("HeaderBrush", Blend(bg, surface, 0.35));
        Set("SidebarBrush", Darken(bg, 0.35));
        Set("ChipBrush", surfaceAlt);
        Set("InputBrush", Blend(bg, surface, 0.45));
        Set("RowAltBrush", Blend(surface, surfaceAlt, 0.55));
        Set("GridLineBrush", Parse(Current.Border, Colors.Gray));
        Set("SelectionBrush", Blend(surfaceAlt, Parse(Current.Accent2, Colors.Blue), 0.35));
        Set("RowHoverBrush", Blend(surface, surfaceAlt, 0.75));

        if (save) Save();
        Changed?.Invoke(Current);
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Si no se puede escribir, la apariencia seguirá activa en esta sesión.
        }
    }

    public static void Reset()
    {
        Apply(AppearanceSettings.ForPreset("Cyber"), save: true);
    }

    private static void Set(string key, Color color)
    {
        if (Application.Current is not { } app) return;
        if (app.TryFindResource(key) is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        app.Resources[key] = new SolidColorBrush(color);
    }

    /// <summary>Claves de orden admitidas por el tablero de equipos.</summary>
    public static IReadOnlyList<string> DashboardSorts { get; } =
        new[] { "manual", "name", "status", "zone", "kind", "time", "amount" };

    public static string NormalizeDashboardSort(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return DashboardSorts.Contains(normalized) ? normalized : "manual";
    }

    public static Color Parse(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return (Color)ColorConverter.ConvertFromString(hex.Trim()); }
        catch { return fallback; }
    }

    public static bool IsValid(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try { ColorConverter.ConvertFromString(hex.Trim()); return true; }
        catch { return false; }
    }

    private static Color Blend(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * amount),
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }

    private static Color Darken(Color color, double amount)
    {
        var factor = 1 - Math.Clamp(amount, 0, 1);
        return Color.FromArgb(color.A, (byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));
    }
}
