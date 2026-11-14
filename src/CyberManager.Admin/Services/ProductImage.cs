using System.IO;
using System.Windows.Media.Imaging;
using CyberManager.Shared;

namespace CyberManager.Admin.Services;

/// <summary>
/// Prepara las imágenes de producto antes de enviarlas al servidor. Los archivos BMP
/// se convierten a PNG para que ocupen menos espacio y se vean igual en todos los
/// equipos; los demás formatos se envían tal cual.
/// </summary>
public static class ProductImage
{
    /// <summary>
    /// Devuelve los bytes y el nombre que se deben subir. Un BMP se recodifica como
    /// PNG conservando su tamaño en píxeles; cualquier otro formato no se modifica.
    /// </summary>
    public static (byte[] Data, string FileName) Normalize(byte[] data, string fileName)
    {
        var probe = ImageInspector.Detect(data);
        if (!probe.Valid || !string.Equals(probe.Extension, ".bmp", StringComparison.OrdinalIgnoreCase))
            return (data, fileName);

        var image = new BitmapImage();
        using (var stream = new MemoryStream(data))
        {
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
        }
        image.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream();
        encoder.Save(output);

        var name = Path.GetFileNameWithoutExtension(fileName);
        return (output.ToArray(), $"{(string.IsNullOrWhiteSpace(name) ? "icono" : name)}.png");
    }
}
