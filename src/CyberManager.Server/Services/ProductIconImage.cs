using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CyberManager.Shared;
using CyberManager.Shared.Imaging;

namespace CyberManager.Server.Services;

/// <summary>
/// Normaliza las imágenes de producto al formato PNG. Todas las imágenes que muestra
/// el software se guardan en PNG, sin importar el formato con el que se subieron.
/// System.Drawing.Common solo está disponible en Windows, que es el único sistema
/// donde se instala el servidor.
/// </summary>
public static class ProductIconImage
{
    /// <summary>
    /// Devuelve la imagen como PNG. Si ya es PNG se conserva tal cual; los BMP, JPG y
    /// GIF se recodifican conservando sus píxeles.
    /// </summary>
#pragma warning disable CA1416 // El servidor solo se ejecuta en Windows.
    public static (byte[] Data, ImageProbe Probe) ToPng(byte[] data)
    {
        var probe = ImageInspector.Detect(data);
        if (!probe.Valid)
            throw new AppException("El archivo no es una imagen válida. Use PNG, JPG, GIF o BMP.");

        if (probe.ContentType == "image/png") return (data, probe);

        try
        {
            using var input = new MemoryStream(data);
            using var image = Image.FromStream(input);
            using var output = new MemoryStream();
            image.Save(output, ImageFormat.Png);

            var png = output.ToArray();
            var pngProbe = ImageInspector.Detect(png);
            if (!pngProbe.Valid)
                throw new AppException("No se pudo convertir la imagen a PNG.");

            return (png, pngProbe);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            throw new AppException("No se pudo convertir la imagen a PNG. Use una imagen válida.");
        }
    }

    /// <summary>
    /// Quita el fondo uniforme de una imagen PNG (por ejemplo el blanco de un logotipo)
    /// y devuelve el PNG con esos píxeles transparentes. Si la imagen no tiene un fondo
    /// claro o algo falla, devuelve la imagen original sin modificar.
    /// </summary>
    public static byte[] RemoveBackground(byte[] png)
    {
        try
        {
            using var input = new MemoryStream(png);
            using var source = new Bitmap(input);

            var width = source.Width;
            var height = source.Height;
            if (width < 3 || height < 3) return png;

            // Se trabaja siempre en BGRA para poder modificar el canal alfa.
            using var bitmap = source.Clone(new Rectangle(0, 0, width, height),
                PixelFormat.Format32bppArgb);

            var rect = new Rectangle(0, 0, width, height);
            var bits = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var stride = bits.Stride;
                var pixels = new byte[stride * height];
                Marshal.Copy(bits.Scan0, pixels, 0, pixels.Length);

                if (!BackgroundRemoval.Apply(pixels, width, height, stride))
                    return png;

                Marshal.Copy(pixels, 0, bits.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bits);
            }

            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
        catch (Exception)
        {
            // Quitar el fondo es una mejora opcional: nunca debe impedir la subida.
            return png;
        }
    }
#pragma warning restore CA1416
}
