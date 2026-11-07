using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CyberManager.Client.Services;

/// <summary>
/// Captura la pantalla del equipo usando GDI y la comprime en JPEG.
/// Se ejecuta en la sesión del usuario, por lo que el agente (servicio) no puede hacerlo.
/// </summary>
internal static class ScreenCapture
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private const uint Srccopy = 0x00CC0020;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr handle);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    /// <summary>Captura todos los monitores y devuelve los bytes JPEG junto al tamaño final.</summary>
    public static (byte[] Jpeg, int Width, int Height) CaptureJpeg(int maxWidth = 1280, int quality = 60)
    {
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);

        // Algunos equipos no reportan la pantalla virtual; se usa la principal como respaldo.
        if (width <= 0 || height <= 0)
        {
            x = 0;
            y = 0;
            width = GetSystemMetrics(SmCxScreen);
            height = GetSystemMetrics(SmCyScreen);
        }

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("No se pudo determinar el tamaño de la pantalla.");

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo acceder a la pantalla del equipo.");

        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);

        try
        {
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, Srccopy))
                throw new InvalidOperationException("No se pudo capturar la pantalla (BitBlt).");

            BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero,
                Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            if (source.PixelWidth > maxWidth && maxWidth > 0)
            {
                var scale = maxWidth / (double)source.PixelWidth;
                source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            }

            source.Freeze();

            var encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 20, 90) };
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return (stream.ToArray(), source.PixelWidth, source.PixelHeight);
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
