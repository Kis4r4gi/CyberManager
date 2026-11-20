using System.Text;

namespace CyberManager.Shared;

/// <summary>
/// Comprueba que un video pueda reproducirse de forma fiable en el reproductor del
/// cliente (Windows Media Foundation). Solo acepta MP4/M4V no fragmentados con
/// vídeo H.264 y resolución hasta 1080p. El análisis se hace en bloques, sin cargar
/// el archivo completo en memoria.
/// </summary>
public static class PromoVideoValidator
{
    /// <summary>Texto que se muestra al usuario cuando el archivo no es compatible.</summary>
    public const string Requirements =
        "Use MP4/M4V con H.264 (AVC), no fragmentado, hasta 1920x1080 y 120 MB. " +
        "Formato por defecto recomendado: preset MP4/H.264 de Format Factory a 1280x720, " +
        "30 fps, unos 1.5 Mbps y sin audio (60 fps se acepta, pero puede ir lento en equipos " +
        "modestos). Ejemplo con ffmpeg: " +
        "ffmpeg -i entrada.mp4 -c:v libx264 -profile:v main -pix_fmt yuv420p " +
        "-r 30 -b:v 1500k -g 60 -an -movflags +faststart salida.mp4";

    private static readonly byte[] Ftyp = Encoding.ASCII.GetBytes("ftyp");
    private static readonly byte[] Moov = Encoding.ASCII.GetBytes("moov");
    private static readonly byte[] Mvex = Encoding.ASCII.GetBytes("mvex");
    private static readonly byte[] Moof = Encoding.ASCII.GetBytes("moof");
    private static readonly byte[] Avc1 = Encoding.ASCII.GetBytes("avc1");
    private static readonly byte[] Avc3 = Encoding.ASCII.GetBytes("avc3");
    private static readonly byte[] Hevc1 = Encoding.ASCII.GetBytes("hvc1");
    private static readonly byte[] Hevc2 = Encoding.ASCII.GetBytes("hev1");
    private static readonly byte[] Av1 = Encoding.ASCII.GetBytes("av01");
    private static readonly byte[] Vp9 = Encoding.ASCII.GetBytes("vp09");
    private static readonly byte[] TkHd = Encoding.ASCII.GetBytes("tkhd");

    private const string FtypName = "ftyp";
    private const string MoovName = "moov";
    private const string MvexName = "mvex";
    private const string MoofName = "moof";
    private const string Avc1Name = "avc1";
    private const string Avc3Name = "avc3";
    private const string Hevc1Name = "hvc1";
    private const string Hevc2Name = "hev1";
    private const string Av1Name = "av01";
    private const string Vp9Name = "vp09";
    private const string TkHdName = "tkhd";

    private static readonly byte[][] Tokens =
    {
        Ftyp, Moov, Mvex, Moof, Avc1, Avc3, Hevc1, Hevc2, Av1, Vp9, TkHd
    };

    /// <summary>
    /// Devuelve <c>null</c> si el video es compatible; si no, devuelve el motivo
    /// junto con las especificaciones admitidas.
    /// </summary>
    public static string? Validate(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase))
        {
            return $"Solo se admiten archivos MP4/M4V. {Requirements}";
        }

        var found = FindTokens(stream, Tokens);

        if (found[FtypName].Count == 0)
            return $"No es un archivo MP4 válido. {Requirements}";

        if (found[MoovName].Count == 0)
            return $"Al MP4 le falta el índice de reproducción. {Requirements}";

        if (found[MvexName].Count > 0 || found[MoofName].Count > 0)
        {
            return "El MP4 está fragmentado (grabación de pantalla o descarga de streaming) " +
                   "y se reproduce entrecortado. " + Requirements;
        }

        if (found[Hevc1Name].Count > 0 || found[Hevc2Name].Count > 0)
            return $"El códec HEVC/H.265 no es compatible. {Requirements}";

        if (found[Av1Name].Count > 0 || found[Vp9Name].Count > 0)
            return $"El códec del video no es compatible. {Requirements}";

        if (found[Avc1Name].Count == 0 && found[Avc3Name].Count == 0)
            return $"Solo se admite vídeo H.264 (AVC). {Requirements}";

        if (stream.CanSeek &&
            found.TryGetValue(TkHdName, out var trackOffsets) && trackOffsets.Count > 0)
        {
            foreach (var offset in trackOffsets)
            {
                var size = ReadTrackSize(stream, offset);
                if (size is null) continue;

                var (width, height) = size.Value;
                if (width > 1920 || height > 1080)
                    return $"La resolución {width}x{height} supera 1920x1080. {Requirements}";
            }
        }

        return null;
    }

    private static Dictionary<string, List<long>> FindTokens(Stream stream, IEnumerable<byte[]> tokens)
    {
        var names = tokens.ToDictionary(t => Encoding.ASCII.GetString(t));
        var result = names.Keys.ToDictionary(n => n, _ => new List<long>(), StringComparer.Ordinal);
        var streamLength = stream.CanSeek ? stream.Length : long.MaxValue;

        const int chunkSize = 64 * 1024;
        const int overlap = 16;
        var buffer = new byte[chunkSize + overlap];
        var carry = 0;
        long streamStart = 0;

        stream.Position = 0;
        while (true)
        {
            var read = stream.Read(buffer, carry, chunkSize);
            if (read == 0) break;

            var total = carry + read;
            var text = Encoding.ASCII.GetString(buffer, 0, total);

            foreach (var (name, _) in names)
            {
                var from = 0;
                int index;
                while ((index = text.IndexOf(name, from, StringComparison.Ordinal)) >= 0)
                {
                    from = index + name.Length;
                    var absolute = streamStart + index;

                    // Los atomos MP4 llevan su tamaño justo antes del nombre. Exigir un
                    // tamaño plausible evita confundir bytes de mdat con un atomo.
                    if (index < 4 || absolute < 4) continue;
                    var boxSize = BigEndian(buffer, index - 4);
                    if (boxSize < 8 || boxSize > streamLength - (absolute - 4)) continue;

                    if (!result[name].Contains(absolute)) result[name].Add(absolute);
                }
            }

            // Los bytes que se conservan para el siguiente bloque empiezan en
            // total - carry; esa es la nueva posición del inicio del búfer.
            carry = Math.Min(overlap, total);
            if (carry > 0) Array.Copy(buffer, total - carry, buffer, 0, carry);
            streamStart += total - carry;
        }

        return result;
    }

    /// <summary>Lee el ancho/alto del atomo tkhd (fixed point 16.16, big endian).</summary>
    private static (int Width, int Height)? ReadTrackSize(Stream stream, long tokenOffset)
    {
        stream.Position = tokenOffset + 4;
        var versionByte = stream.ReadByte();
        if (versionByte < 0) return null;

        var fields = versionByte switch
        {
            0 => tokenOffset + 80,
            1 => tokenOffset + 92,
            _ => -1
        };
        if (fields < 0) return null;

        var bytes = new byte[8];
        stream.Position = fields;
        if (stream.Read(bytes, 0, bytes.Length) != bytes.Length) return null;

        var width = (int)(BigEndian(bytes, 0) >> 16);
        var height = (int)(BigEndian(bytes, 4) >> 16);
        return width > 0 && height > 0 ? (width, height) : null;
    }

    private static uint BigEndian(byte[] bytes, int offset)
        => ((uint)bytes[offset] << 24) |
           ((uint)bytes[offset + 1] << 16) |
           ((uint)bytes[offset + 2] << 8) |
           bytes[offset + 3];
}
