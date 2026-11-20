using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CyberManager.Client;

/// <summary>
/// Reúne datos del equipo y del render de WPF para entender por qué un video se
/// reproduce lento (cámara lenta o a tirones). El resumen viaja en el latido y el
/// servidor lo registra, de modo que el soporte puede verlo sin acceder a la
/// estación. Solo contiene texto; no afecta a la reproducción.
/// </summary>
internal static class ClientDiagnostics
{
    private static string? _hardware;
    private static string? _media;

    /// <summary>Resumen estático del equipo: nivel de render, GPU, DPI, pantalla y sistema.</summary>
    public static string Hardware => _hardware ??= BuildHardware();

    /// <summary>Estado del video en curso; <c>null</c> cuando no hay ninguno.</summary>
    public static string? Media
    {
        get => _media;
        set => _media = value;
    }

    /// <summary>Resumen que se envía en el latido: equipo + video actual (si lo hay).</summary>
    public static string Snapshot()
    {
        var media = _media;
        return string.IsNullOrEmpty(media) ? Hardware : $"{Hardware} | video: {media}";
    }

    private static string BuildHardware()
    {
        // Nivel de render de WPF: 0 = sin aceleración por hardware (software), 2 = completo.
        // Es el dato decisivo para saber si el problema es la GPU o el propio reproductor.
        var tier = -1;
        try { tier = RenderCapability.Tier >> 16; } catch { /* sin contexto gráfico */ }

        var screen = "?";
        var dpi = 0;
        try
        {
            screen = $"{SystemParameters.PrimaryScreenWidth:0}x{SystemParameters.PrimaryScreenHeight:0}";
            var source = PresentationSource.FromVisual(Application.Current?.MainWindow);
            if (source?.CompositionTarget is not null)
                dpi = (int)Math.Round(96 * source.CompositionTarget.TransformToDevice.M11);
        }
        catch { /* sin ventana todavía */ }

        return $"tier={tier} gpu={Gpu()} dpi={dpi} pantalla={screen} " +
               $"hz={RefreshHz()} bpp={DeviceCap(12)} os={Environment.OSVersion.VersionString}";
    }

    /// <summary>
    /// Frecuencia de refresco real del monitor en Hz. Es el tope físico de fluidez:
    /// si el monitor está a 30 Hz (o el equipo va por escritorio remoto), ningún
    /// reproductor puede mostrar más de 30 fotogramas por segundo.
    /// </summary>
    private static int RefreshHz() => DeviceCap(116); // VREFRESH

    private static int DeviceCap(int index)
    {
        var dc = IntPtr.Zero;
        try
        {
            dc = GetDC(IntPtr.Zero);
            return dc == IntPtr.Zero ? -1 : GetDeviceCaps(dc, index);
        }
        catch
        {
            return -1;
        }
        finally
        {
            if (dc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, dc);
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);

    /// <summary>Nombre del controlador de video desde el registro (no requiere WMI).</summary>
    private static string Gpu()
    {
        try
        {
            const string root =
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            using var key = Registry.LocalMachine.OpenSubKey(root);
            if (key is null) return "?";

            var names = new List<string>();
            foreach (var sub in key.GetSubKeyNames())
            {
                // Las instancias reales son 0000, 0001, ...
                if (sub.Length != 4 || !sub.All(char.IsDigit)) continue;

                using var device = key.OpenSubKey(sub);
                if (device?.GetValue("DriverDesc") is string desc && !string.IsNullOrWhiteSpace(desc))
                    names.Add(desc.Trim());
            }

            return names.Count == 0 ? "?" : string.Join(" / ", names.Distinct());
        }
        catch
        {
            return "?";
        }
    }
}
