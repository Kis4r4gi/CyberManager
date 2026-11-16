using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CyberManager.Shared;

namespace CyberManager.Admin.Services;

/// <summary>
/// Convierte el logo del negocio entre un archivo de imagen y la data URL que
/// se guarda en el servidor, y decodifica el logo para mostrarlo en la consola.
/// </summary>
public static class Branding
{
    /// <summary>Lado máximo del logo una vez reescalado.</summary>
    public const int MaxLogoSize = 512;

    /// <summary>
    /// Lee una imagen, la reescala a un máximo de 512 × 512 px conservando la
    /// proporción y la devuelve como data URL PNG.
    /// </summary>
    public static string LoadLogoDataUrl(string filePath)
    {
        if (!ImageInspector.IsAllowedFileName(filePath))
            throw new InvalidOperationException("Formato no compatible. Use PNG, JPG, GIF o BMP.");

        var file = new FileInfo(filePath);
        if (!file.Exists) throw new FileNotFoundException("No se encontró el archivo seleccionado.", filePath);
        if (file.Length > ImageInspector.MaxBytes)
            throw new InvalidOperationException("La imagen supera el límite de 20 MB.");

        var data = File.ReadAllBytes(filePath);
        if (!ImageInspector.Detect(data).Valid)
            throw new InvalidOperationException("El archivo seleccionado no es una imagen válida.");

        using var input = new MemoryStream(data);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
            throw new InvalidOperationException("La imagen no tiene contenido.");

        BitmapSource source = decoder.Frames[0];
        var largestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        if (largestSide > MaxLogoSize)
        {
            var scale = (double)MaxLogoSize / largestSide;
            var resized = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            resized.Freeze();
            source = resized;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var output = new MemoryStream();
        encoder.Save(output);
        return "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
    }

    /// <summary>Decodifica una data URL en una imagen congelada lista para WPF.</summary>
    public static BitmapImage? Decode(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;

        try
        {
            var comma = dataUrl.IndexOf(',');
            if (comma < 0) return null;

            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
