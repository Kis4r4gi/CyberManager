namespace CyberManager.Shared;

/// <summary>Resultado de inspeccionar una imagen sin depender de System.Drawing.</summary>
public sealed record ImageProbe(bool Valid, string ContentType, string Extension, int Width, int Height)
{
    public static readonly ImageProbe Invalid = new(false, string.Empty, string.Empty, 0, 0);
}

/// <summary>Detecta el formato y las dimensiones de PNG, JPEG, GIF y BMP por sus cabeceras.</summary>
public static class ImageInspector
{
    public const int MaxBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp"
    };

    public static bool IsAllowedFileName(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
           && AllowedExtensions.Contains(Path.GetExtension(fileName));

    public static ImageProbe Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10) return ImageProbe.Invalid;

        var png = DetectPng(data);
        if (png.Valid) return png;

        var jpeg = DetectJpeg(data);
        if (jpeg.Valid) return jpeg;

        var gif = DetectGif(data);
        if (gif.Valid) return gif;

        var bmp = DetectBmp(data);
        if (bmp.Valid) return bmp;

        return ImageProbe.Invalid;
    }

    private static ImageProbe DetectPng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24) return ImageProbe.Invalid;
        if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47
            || data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A)
            return ImageProbe.Invalid;

        // El bloque IHDR siempre es el primero y contiene ancho/alto en big-endian.
        var width = BigEndianInt32(data.Slice(16, 4));
        var height = BigEndianInt32(data.Slice(20, 4));
        return Dimensions("image/png", ".png", width, height);
    }

    private static ImageProbe DetectJpeg(ReadOnlySpan<byte> data)
    {
        if (data[0] != 0xFF || data[1] != 0xD8) return ImageProbe.Invalid;

        var i = 2;
        while (i + 3 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }

            // Marcadores sin longitud.
            if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
            {
                i += 2;
                continue;
            }

            var length = (data[i + 2] << 8) | data[i + 3];
            if (length < 2) return ImageProbe.Invalid;

            var isStartOfFrame =
                (marker >= 0xC0 && marker <= 0xC3) ||
                (marker >= 0xC5 && marker <= 0xC7) ||
                (marker >= 0xC9 && marker <= 0xCB) ||
                (marker >= 0xCD && marker <= 0xCF);

            if (isStartOfFrame)
            {
                if (i + 8 >= data.Length) return ImageProbe.Invalid;
                var height = (data[i + 5] << 8) | data[i + 6];
                var width = (data[i + 7] << 8) | data[i + 8];
                return Dimensions("image/jpeg", ".jpg", width, height);
            }

            i += 2 + length;
        }

        return ImageProbe.Invalid;
    }

    private static ImageProbe DetectGif(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10) return ImageProbe.Invalid;
        if (data[0] != (byte)'G' || data[1] != (byte)'I' || data[2] != (byte)'F'
            || data[3] != (byte)'8' || (data[4] != (byte)'7' && data[4] != (byte)'9')
            || data[5] != (byte)'a')
            return ImageProbe.Invalid;

        var width = data[6] | (data[7] << 8);
        var height = data[8] | (data[9] << 8);
        return Dimensions("image/gif", ".gif", width, height);
    }

    private static ImageProbe DetectBmp(ReadOnlySpan<byte> data)
    {
        if (data.Length < 26) return ImageProbe.Invalid;
        if (data[0] != (byte)'B' || data[1] != (byte)'M') return ImageProbe.Invalid;

        var width = LittleEndianInt32(data.Slice(18, 4));
        var height = LittleEndianInt32(data.Slice(22, 4));
        return Dimensions("image/bmp", ".bmp", width, Math.Abs(height));
    }

    private static ImageProbe Dimensions(string contentType, string extension, int width, int height)
        => width > 0 && height > 0 && width <= 20000 && height <= 20000
            ? new ImageProbe(true, contentType, extension, width, height)
            : ImageProbe.Invalid;

    private static int BigEndianInt32(ReadOnlySpan<byte> data)
        => (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];

    private static int LittleEndianInt32(ReadOnlySpan<byte> data)
        => data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24);
}
