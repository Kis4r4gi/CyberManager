using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CyberManager.Shared;

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
#pragma warning restore CA1416
}
