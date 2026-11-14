using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CyberManager.Shared;

namespace CyberManager.Admin.Services;

/// <summary>
/// Prepara las imágenes de producto antes de enviarlas al servidor. Todas las
/// imágenes se guardan en PNG: los BMP, JPG y GIF se recodifican conservando sus
/// píxeles, y los PNG se envían tal cual.
/// </summary>
public static class ProductImage
{
    /// <summary>
    /// Devuelve los bytes y el nombre que se deben subir. Si la imagen no es PNG se
    /// recodifica como PNG; el nombre también cambia a la extensión <c>.png</c>.
    /// </summary>
    public static (byte[] Data, string FileName) Normalize(byte[] data, string fileName)
    {
        var probe = ImageInspector.Detect(data);
        if (!probe.Valid || probe.ContentType == "image/png") return (data, fileName);

        var png = EncodePng(data, maxSize: 0);
        var name = Path.GetFileNameWithoutExtension(fileName);
        return (png, $"{(string.IsNullOrWhiteSpace(name) ? "icono" : name)}.png");
    }

    /// <summary>
    /// Convierte cualquier imagen a PNG. Si <paramref name="maxSize"/> es mayor que
    /// cero, reduce la imagen para que su lado mayor no supere ese tamaño.
    /// </summary>
    public static byte[] ToPng(byte[] data, int maxSize = 0)
    {
        if (!ImageInspector.Detect(data).Valid)
            throw new InvalidOperationException("El archivo no es una imagen válida.");
        return EncodePng(data, maxSize);
    }

    /// <summary>Decodifica una imagen en memoria sin bloquear el archivo de origen.</summary>
    public static BitmapSource Decode(byte[] data)
    {
        var image = new BitmapImage();
        using (var stream = new MemoryStream(data))
        {
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
        }
        image.Freeze();
        return image;
    }

    private static byte[] EncodePng(byte[] data, int maxSize)
    {
        var image = Decode(data);

        BitmapSource source = image;
        var currentMax = Math.Max(image.PixelWidth, image.PixelHeight);
        if (maxSize > 0 && currentMax > maxSize)
        {
            var scale = (double)maxSize / currentMax;
            source = new TransformedBitmap(image, new ScaleTransform(scale, scale));
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
