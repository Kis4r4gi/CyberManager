using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CyberManager.Admin.Services;

/// <summary>
/// Utilidades para la barra de título nativa de Windows y para encuadrar la ventana
/// dentro del área de trabajo (sin quedar debajo de la barra de tareas).
/// </summary>
public static class NativeWindow
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Pinta la barra de título nativa en tono oscuro para que los iconos sean visibles.</summary>
    public static void ApplyDarkTitleBar(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            var enabled = 1;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));

            // Windows 11 permite igualar la barra con el encabezado de la aplicación.
            var caption = 0x0026150B; // #0B1526 en formato COLORREF (BBGGRR)
            DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, sizeof(int));

            var text = 0x00FAEFE8; // #E8EFFA
            DwmSetWindowAttribute(handle, DwmwaTextColor, ref text, sizeof(int));
        }
        catch
        {
            // En versiones antiguas de Windows basta con la barra nativa estándar.
        }
    }

    /// <summary>
    /// Ajusta la ventana al área de trabajo visible para que nunca quede debajo de la barra de tareas.
    /// </summary>
    public static void FitToWorkArea(Window window, double margin = 12, bool centerOnScreen = true)
    {
        if (window.WindowState != WindowState.Normal) return;

        var area = SystemParameters.WorkArea;
        if (area.Width <= 0 || area.Height <= 0) return;

        var maxWidth = Math.Max(480, area.Width - margin * 2);
        var maxHeight = Math.Max(360, area.Height - margin * 2);

        if (window.MinWidth > maxWidth) window.MinWidth = maxWidth;
        if (window.MinHeight > maxHeight) window.MinHeight = maxHeight;

        var width = Math.Min(Math.Max(window.Width, window.MinWidth), maxWidth);
        var height = Math.Min(Math.Max(window.Height, window.MinHeight), maxHeight);

        window.Width = width;
        window.Height = height;

        if (!centerOnScreen) return;

        window.Left = area.Left + (area.Width - width) / 2;
        window.Top = area.Top + (area.Height - height) / 2;
    }
}
