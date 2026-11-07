using System.IO;
using System.Windows.Threading;
using CyberManager.Desktop.Api;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

/// <summary>
/// Descarga y cachea los videos promocionales que el servidor asigna a los equipos
/// bloqueados. Los archivos se guardan con el hash SHA-256 como nombre para
/// reutilizarlos entre reinicios y se eliminan cuando dejan de estar publicados.
/// </summary>
public sealed class PromoVideoService : IDisposable
{
    private readonly string _cacheDir;
    private readonly Dictionary<int, string> _files = new();
    private readonly List<string> _playlist = new();

    private IReadOnlyList<PromoVideoItemDto> _items = Array.Empty<PromoVideoItemDto>();
    private string _revision = string.Empty;
    private int _index;
    private int _version;
    private bool _locked;

    /// <summary>Ruta local del video que debe reproducirse; <c>null</c> oculta el reproductor.</summary>
    public event Action<string?>? VideoChanged;

    public PromoVideoService()
    {
        _cacheDir = Path.Combine(AppPaths.DataDir("Client"), "promos");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task ApplyAsync(PromoVideoPlaylistDto? playlist)
    {
        var version = ++_version;

        if (playlist is null || playlist.Items.Count == 0)
        {
            _revision = playlist?.Revision ?? string.Empty;
            _items = Array.Empty<PromoVideoItemDto>();
            _playlist.Clear();
            _index = 0;
            VideoChanged?.Invoke(null);
            CleanCache();
            return;
        }

        var changed = !string.Equals(_revision, playlist.Revision, StringComparison.Ordinal);
        _revision = playlist.Revision;

        if (!changed && _items.Count == playlist.Items.Count && _playlist.Count > 0)
        {
            ShowCurrent();
            return;
        }

        _items = playlist.Items;
        _playlist.Clear();
        _index = 0;

        foreach (var item in _items)
        {
            if (version != _version) return;
            try
            {
                var path = await EnsureFileAsync(item);
                if (path is not null) _playlist.Add(path);
            }
            catch
            {
                // Un video dañado no debe impedir que se reproduzcan los demás.
            }
        }

        if (version != _version) return;
        ShowCurrent();
        CleanCache();
    }

    public void SetLocked(bool locked)
    {
        _locked = locked;
        if (locked) ShowCurrent();
        else VideoChanged?.Invoke(null);
    }

    /// <summary>Avanza al siguiente video. Se llama cuando termina el actual.</summary>
    public void Next()
    {
        if (_playlist.Count == 0)
        {
            VideoChanged?.Invoke(null);
            return;
        }

        _index = (_index + 1) % _playlist.Count;
        ShowCurrent();
    }

    private async Task<string?> EnsureFileAsync(PromoVideoItemDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Sha256)) return null;

        var path = Path.Combine(_cacheDir, item.Sha256 + item.Extension);
        _files[item.Id] = path;

        if (File.Exists(path))
        {
            var length = new FileInfo(path).Length;
            if (length == item.SizeBytes || item.SizeBytes <= 0) return path;
            TryDelete(path);
        }

        if (App.Api is null) return null;

        var data = await App.Api.PromoVideoFileAsync(item.Id);
        if (data.Length == 0) return null;

        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, data);

        try
        {
            File.Move(temp, path, overwrite: true);
            return path;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private void ShowCurrent()
    {
        if (!_locked || _playlist.Count == 0)
        {
            VideoChanged?.Invoke(null);
            return;
        }

        _index %= _playlist.Count;
        var path = _playlist[_index];
        if (!File.Exists(path))
        {
            _playlist.RemoveAt(_index);
            Next();
            return;
        }

        VideoChanged?.Invoke(path);
    }

    /// <summary>Borra videos que ya no están publicados por el servidor.</summary>
    private void CleanCache()
    {
        var keep = _items
            .Select(i => i.Sha256 + i.Extension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var file in Directory.EnumerateFiles(_cacheDir))
            {
                var name = Path.GetFileName(file);
                if (!keep.Contains(name)) TryDelete(file);
            }
        }
        catch
        {
            // La caché se limpiará en el siguiente intento.
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* archivo en uso */ }
    }

    public void Dispose()
    {
        _items = Array.Empty<PromoVideoItemDto>();
        _files.Clear();
        _playlist.Clear();
        VideoChanged = null;
    }
}
