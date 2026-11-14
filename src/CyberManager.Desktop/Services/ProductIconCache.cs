using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CyberManager.Desktop.Api;

namespace CyberManager.Desktop.Services;

/// <summary>
/// Caché de las imágenes de producto. La base de datos es la fuente de verdad; cada
/// equipo conserva una copia en disco y en memoria para no descargar la misma imagen
/// cada vez que se abre la caja o la pantalla del cliente.
/// </summary>
public sealed class ProductIconCache
{
    private readonly string _folder;
    private readonly Dictionary<string, ImageSource> _memory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<ImageSource?>> _inflight = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public ProductIconCache(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
    }

    /// <summary>Crea la caché dentro de los datos locales de la aplicación indicada.</summary>
    public static ProductIconCache Create(string app)
        => new(Path.Combine(AppPaths.DataDir(app), "icons"));

    /// <summary>
    /// Devuelve la imagen del producto. <paramref name="updatedUtc"/> funciona como marca
    /// de versión: si cambia, se descarga la imagen nueva y se descarta la anterior.
    /// Devuelve null si el producto no tiene imagen o si la descarga falla.
    /// </summary>
    public Task<ImageSource?> GetAsync(int productId, DateTime? updatedUtc,
        Func<int, Task<byte[]>> download)
    {
        if (updatedUtc is not DateTime stamp) return Task.FromResult<ImageSource?>(null);

        // El servidor envía la marca en UTC; si llega sin zona se interpreta como UTC
        // para que la clave no cambie con la zona horaria del equipo.
        var stampUtc = stamp.Kind == DateTimeKind.Utc
            ? stamp
            : DateTime.SpecifyKind(stamp, DateTimeKind.Utc);
        var key = $"{productId}-{stampUtc:yyyyMMddHHmmssfff}";

        lock (_gate)
        {
            if (_memory.TryGetValue(key, out var ready)) return Task.FromResult<ImageSource?>(ready);
            if (_inflight.TryGetValue(key, out var pending)) return pending;

            var task = LoadAsync(key, productId, download);
            _inflight[key] = task;
            _ = task.ContinueWith(_ =>
            {
                lock (_gate) _inflight.Remove(key);
            }, TaskScheduler.Default);
            return task;
        }
    }

    private async Task<ImageSource?> LoadAsync(string key, int productId,
        Func<int, Task<byte[]>> download)
    {
        try
        {
            var path = Path.Combine(_folder, key + ".img");
            ImageSource? image = null;

            if (File.Exists(path))
            {
                try { image = Load(path); }
                catch { TryDelete(path); }
            }

            if (image is null)
            {
                var data = await download(productId);
                image = Load(data);
                try { await File.WriteAllBytesAsync(path, data); }
                catch { /* si no se puede escribir, igual se muestra desde memoria */ }
            }

            lock (_gate)
            {
                // Descarta de memoria las versiones anteriores de este mismo producto.
                foreach (var stale in _memory.Keys.Where(k => k.StartsWith($"{productId}-", StringComparison.Ordinal)).ToList())
                {
                    if (!string.Equals(stale, key, StringComparison.Ordinal)) _memory.Remove(stale);
                }

                _memory[key] = image;
            }

            PruneOld(productId, key);
            return image;
        }
        catch
        {
            // Sin conexión o imagen inválida: la interfaz conserva el emoji de respaldo.
            return null;
        }
    }

    private void PruneOld(int productId, string keepKey)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_folder, $"{productId}-*.img"))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(file), keepKey, StringComparison.Ordinal))
                    continue;
                TryDelete(file);
            }
        }
        catch
        {
            // La limpieza es opcional; nunca debe impedir que se muestre la imagen.
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* archivo en uso */ }
    }

    private static ImageSource Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    private static ImageSource Load(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Load(stream);
    }

    private static ImageSource Load(Stream stream)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        // No se usa BitmapCreateOptions.IgnoreImageCache: en WPF (.NET 8) provoca
        // ArgumentNullException al decodificar desde un StreamSource. La caché ya
        // usa nombres por versión, así que no hace falta invalidar la global.
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
