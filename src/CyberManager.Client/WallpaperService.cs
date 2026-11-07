using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberManager.Desktop.Api;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

/// <summary>
/// Descarga, cachea y rota los fondos que el servidor asigna a la pantalla de bloqueo.
/// Las imágenes se guardan en %LOCALAPPDATA%\CyberManager\Client\wallpapers usando el
/// hash SHA-256 como nombre, de modo que se reutilizan entre reinicios.
/// </summary>
public sealed class WallpaperService : IDisposable
{
    private readonly string _cacheDir;
    private readonly DispatcherTimer _rotationTimer = new();
    private readonly Dictionary<int, string> _files = new();
    private readonly Dictionary<int, ImageSource> _images = new();

    private IReadOnlyList<WallpaperItemDto> _items = Array.Empty<WallpaperItemDto>();
    private string _revision = string.Empty;
    private int _index;
    private int _seconds = WallpaperSettings.DefaultRotationSeconds;
    private bool _locked;
    private int _version;

    /// <summary>Imagen que debe mostrarse; <c>null</c> significa ocultar el fondo.</summary>
    public event Action<ImageSource?>? ImageChanged;

    public WallpaperService()
    {
        _cacheDir = Path.Combine(AppPaths.DataDir("Client"), "wallpapers");
        Directory.CreateDirectory(_cacheDir);
        _rotationTimer.Tick += (_, _) => Next();
    }

    /// <summary>Aplica la lista recibida del servidor y descarga las imágenes que falten.</summary>
    public async Task ApplyAsync(WallpaperPlaylistDto? playlist)
    {
        var version = ++_version;

        if (playlist is null || playlist.Mode == WallpaperMode.None || playlist.Items.Count == 0)
        {
            _revision = playlist?.Revision ?? string.Empty;
            _items = Array.Empty<WallpaperItemDto>();
            _index = 0;
            StopRotation();
            ImageChanged?.Invoke(null);
            return;
        }

        var changed = !string.Equals(_revision, playlist.Revision, StringComparison.Ordinal);
        _revision = playlist.Revision;
        _seconds = Math.Clamp(playlist.RotationSeconds,
            WallpaperSettings.MinRotationSeconds, WallpaperSettings.MaxRotationSeconds);

        // Si la lista no cambió y la imagen actual ya está en disco, no hay nada que descargar.
        if (!changed && _images.Count > 0 && _items.Count == playlist.Items.Count)
        {
            ShowCurrent();
            ConfigureRotation();
            return;
        }

        _items = playlist.Items;
        _index = 0;

        foreach (var item in _items)
        {
            if (version != _version) return;
            try
            {
                await EnsureFileAsync(item);
            }
            catch
            {
                // Una imagen dañada no debe impedir que se muestre el resto de la galería.
            }
        }

        if (version != _version) return;

        ShowCurrent();
        ConfigureRotation();
    }

    /// <summary>Pausa la rotación cuando el equipo se desbloquea.</summary>
    public void SetLocked(bool locked)
    {
        _locked = locked;
        if (locked)
        {
            ShowCurrent();
            ConfigureRotation();
        }
        else
        {
            StopRotation();
        }
    }

    private async Task EnsureFileAsync(WallpaperItemDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Sha256)) return;

        var path = Path.Combine(_cacheDir, item.Sha256 + item.Extension);
        _files[item.Id] = path;

        if (File.Exists(path))
        {
            var length = new FileInfo(path).Length;
            if (length == item.SizeBytes || item.SizeBytes <= 0)
                return;
            TryDelete(path);
        }

        if (App.Api is null) return;

        var data = await App.Api.WallpaperFileAsync(item.Id);
        if (data.Length == 0) return;

        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, data);

        try
        {
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private void ShowCurrent()
    {
        if (!_locked)
        {
            ImageChanged?.Invoke(null);
            return;
        }

        if (_items.Count == 0)
        {
            ImageChanged?.Invoke(null);
            return;
        }

        // Salta fondos que no pudieron descargarse.
        for (var attempt = 0; attempt < _items.Count; attempt++)
        {
            var item = _items[_index % _items.Count];
            if (_images.TryGetValue(item.Id, out var cached))
            {
                ImageChanged?.Invoke(cached);
                return;
            }

            if (!_files.TryGetValue(item.Id, out var path) || !File.Exists(path))
            {
                _index = (_index + 1) % _items.Count;
                continue;
            }

            try
            {
                var image = Load(path);
                _images[item.Id] = image;
                ImageChanged?.Invoke(image);
                return;
            }
            catch
            {
                _index = (_index + 1) % _items.Count;
            }
        }

        ImageChanged?.Invoke(null);
    }

    private void Next()
    {
        if (_items.Count == 0) return;
        _index = (_index + 1) % _items.Count;
        ShowCurrent();
    }

    private void ConfigureRotation()
    {
        if (!_locked || _items.Count < 2)
        {
            StopRotation();
            return;
        }

        _rotationTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, _seconds));
        if (!_rotationTimer.IsEnabled) _rotationTimer.Start();
    }

    private void StopRotation() => _rotationTimer.Stop();

    private static ImageSource Load(string path)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* archivo en uso */ }
    }

    public void Dispose()
    {
        StopRotation();
        _images.Clear();
        _files.Clear();
        _items = Array.Empty<WallpaperItemDto>();
    }
}
