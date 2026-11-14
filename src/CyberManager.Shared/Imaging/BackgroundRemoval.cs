namespace CyberManager.Shared.Imaging;

/// <summary>
/// Quita el fondo uniforme de una imagen de producto (por ejemplo el blanco de un
/// logotipo o de una fotografía recortada). Trabaja sobre píxeles BGRA en memoria:
/// rellena desde los bordes los píxeles parecidos al color del borde y los vuelve
/// transparentes. Si el borde no es uniforme (una fotografía normal) no cambia nada.
/// </summary>
public static class BackgroundRemoval
{
    /// <summary>Diferencia máxima entre el color del borde y el píxel analizado.</summary>
    public const int DefaultTolerance = 44;

    /// <summary>Se omiten imágenes enormes para no consumir memoria de más.</summary>
    private const int MaxPixels = 24_000_000;

    /// <summary>
    /// Aplica la transparencia sobre <paramref name="pixels"/> (BGRA de 4 bytes por
    /// píxel) y devuelve <c>true</c> si modificó la imagen.
    /// </summary>
    public static bool Apply(byte[] pixels, int width, int height, int stride,
        int tolerance = DefaultTolerance)
    {
        if (width < 3 || height < 3) return false;
        if (stride < width * 4) return false;
        if (pixels.Length < stride * height) return false;
        if ((long)width * height > MaxPixels) return false;

        // ---------- 1. Color de referencia y uniformidad del borde ----------
        long sumB = 0, sumG = 0, sumR = 0;
        var opaque = 0;
        var transparent = 0;

        void Sample(int x, int y)
        {
            var i = y * stride + x * 4;
            if (pixels[i + 3] < 16)
            {
                transparent++;
                return;
            }

            sumB += pixels[i];
            sumG += pixels[i + 1];
            sumR += pixels[i + 2];
            opaque++;
        }

        for (var x = 0; x < width; x++)
        {
            Sample(x, 0);
            Sample(x, height - 1);
        }

        for (var y = 1; y < height - 1; y++)
        {
            Sample(0, y);
            Sample(width - 1, y);
        }

        var border = opaque + transparent;
        if (opaque == 0) return false;

        // Un borde ya transparente indica que la imagen viene recortada.
        if (transparent * 100 >= border * 25) return false;

        var averageB = (int)(sumB / opaque);
        var averageG = (int)(sumG / opaque);
        var averageR = (int)(sumR / opaque);
        var toleranceSquared = tolerance * tolerance;

        var matching = 0;

        void Count(int x, int y)
        {
            var i = y * stride + x * 4;
            if (pixels[i + 3] < 16) return;
            if (DistanceSquared(pixels, i, averageB, averageG, averageR) <= toleranceSquared)
                matching++;
        }

        for (var x = 0; x < width; x++)
        {
            Count(x, 0);
            Count(x, height - 1);
        }

        for (var y = 1; y < height - 1; y++)
        {
            Count(0, y);
            Count(width - 1, y);
        }

        // Menos del 70 % del borde con el mismo color: es una fotografía, no un fondo.
        if (matching * 100 < opaque * 70) return false;

        // ---------- 2. Relleno desde los bordes ----------
        var total = width * height;
        var visited = new bool[total];
        var pending = new Stack<int>();

        void Push(int index)
        {
            if (visited[index]) return;

            var i = (index / width) * stride + (index % width) * 4;
            if (pixels[i + 3] < 16)
            {
                // Ya era transparente: se marca para seguir avanzando.
                visited[index] = true;
                pending.Push(index);
                return;
            }

            if (DistanceSquared(pixels, i, averageB, averageG, averageR) > toleranceSquared)
                return;

            visited[index] = true;
            pending.Push(index);
        }

        for (var x = 0; x < width; x++)
        {
            Push(x);
            Push((height - 1) * width + x);
        }

        for (var y = 1; y < height - 1; y++)
        {
            Push(y * width);
            Push(y * width + width - 1);
        }

        while (pending.Count > 0)
        {
            var index = pending.Pop();
            var x = index % width;
            var y = index / width;

            if (x > 0) Push(index - 1);
            if (x < width - 1) Push(index + 1);
            if (y > 0) Push(index - width);
            if (y < height - 1) Push(index + width);
        }

        var background = 0;
        for (var i = 0; i < total; i++)
            if (visited[i]) background++;

        // Casi toda la imagen es del color del borde: probablemente no es un fondo.
        if (background == 0 || background * 100 >= total * 98) return false;

        var changed = 0;
        for (var index = 0; index < total; index++)
        {
            if (!visited[index]) continue;

            var i = (index / width) * stride + (index % width) * 4;
            if (pixels[i + 3] == 0) continue;

            pixels[i + 3] = 0;
            changed++;
        }

        return changed > 0;
    }

    private static int DistanceSquared(byte[] pixels, int i, int b, int g, int r)
    {
        var db = pixels[i] - b;
        var dg = pixels[i + 1] - g;
        var dr = pixels[i + 2] - r;
        return db * db + dg * dg + dr * dr;
    }
}
