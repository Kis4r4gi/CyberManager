using System.IO;
using System.Text.Json;
using System.Windows;
using CyberManager.Desktop.Api;

namespace CyberManager.Admin.Services;

/// <summary>Posición y tamaño recordados de una ventana.</summary>
public sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>
/// Guarda la posición y el tamaño de las ventanas de la consola en un archivo
/// local, separado por usuario. La ventana principal usa el usuario de la
/// aplicación y el inicio de sesión usa el usuario de Windows.
/// </summary>
public static class WindowPlacementStore
{
    private static string FilePath => Path.Combine(AppPaths.DataDir("Admin"), "windows.json");

    private static Dictionary<string, WindowPlacement> LoadStore()
    {
        try
        {
            if (!File.Exists(FilePath)) return new(StringComparer.OrdinalIgnoreCase);

            var store = JsonSerializer.Deserialize<Dictionary<string, WindowPlacement>>(File.ReadAllText(FilePath))
                        ?? new Dictionary<string, WindowPlacement>(StringComparer.OrdinalIgnoreCase);

            // Descarte entradas inválidas de versiones anteriores del archivo.
            foreach (var key in store.Where(kv => kv.Value is null || kv.Value.Width <= 0 || kv.Value.Height <= 0)
                         .Select(kv => kv.Key).ToList())
                store.Remove(key);

            return store;
        }
        catch
        {
            // Un archivo dañado no debe impedir que se abra la consola.
            return new Dictionary<string, WindowPlacement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static WindowPlacement? Load(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return LoadStore().TryGetValue(key, out var placement) ? placement : null;
    }

    public static void Save(string key, WindowPlacement placement)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            var store = LoadStore();
            store[key] = placement;

            var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Si no se puede escribir, la ventana se abrirá centrada la próxima vez.
        }
    }

    /// <summary>Guarda el estado actual de la ventana; si está maximizada, conserva su tamaño normal.</summary>
    public static void SaveWindow(string key, Window window)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;

        Save(key, new WindowPlacement
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = window.WindowState == WindowState.Maximized
        });
    }
}
