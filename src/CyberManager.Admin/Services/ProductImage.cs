using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CyberManager.Shared;
using CyberManager.Shared.Imaging;

namespace CyberManager.Admin.Services;

/// <summary>
/// Prepara las imágenes de producto antes de enviarlas al servidor. Todas las
/// imágenes se guardan en PNG: los BMP, JPG y GIF se recodifican conservando sus
/// píxeles, y se quita el fondo uniforme para que se integren con el tema.
/// </summary>
public static class ProductImage
{
    /// <summary>
    /// Devuelve los bytes y el nombre que se deben subir. Si la imagen no es PNG se
    /// recodifica como PNG; el nombre también cambia a la extensión <c>.png</c>.
    /// Además se quita el fondo uniforme (por ejemplo el blanco de un logotipo).
    /// </summary>
    public static (byte[] Data, string FileName) Normalize(byte[] data, string fileName)
    {
        var probe = ImageInspector.Detect(data);
        if (!probe.Valid) return (data, fileName);

        var isPng = probe.ContentType == "image/png";
        var png = isPng ? data : EncodePng(data, maxSize: 0);
        var cleaned = RemoveBackground(png);

        // Si no hubo cambios se conserva el nombre original.
        if (isPng && ReferenceEquals(cleaned, png)) return (data, fileName);

        var name = Path.GetFileNameWithoutExtension(fileName);
        return (cleaned, $"{(string.IsNullOrWhiteSpace(name) ? "icono" : name)}.png");
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

    /// <summary>
    /// Quita el fondo uniforme de un PNG (el blanco de un logotipo, por ejemplo) y lo
    /// devuelve con esos píxeles transparentes. Si no detecta un fondo claro o algo
    /// falla, devuelve los mismos bytes sin modificar.
    /// </summary>
    public static byte[] RemoveBackground(byte[] png)
    {
        try
        {
            var source = Decode(png);
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            if (width < 3 || height < 3) return png;

            // Bgra32 es el formato que entiende el algoritmo de fondo.
            var converted = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            var stride = width * 4;
            var pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            if (!BackgroundRemoval.Apply(pixels, width, height, stride)) return png;

            var cleaned = BitmapSource.Create(width, height,
                source.DpiX > 0 ? source.DpiX : 96,
                source.DpiY > 0 ? source.DpiY : 96,
                PixelFormats.Bgra32, null, pixels, stride);
            cleaned.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(cleaned));
            using var output = new MemoryStream();
            encoder.Save(output);
            return output.ToArray();
        }
        catch
        {
            // Quitar el fondo es una mejora opcional: nunca debe impedir la subida.
            return png;
        }
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
