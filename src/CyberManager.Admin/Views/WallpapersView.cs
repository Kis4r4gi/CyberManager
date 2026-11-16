using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.Win32;

namespace CyberManager.Admin.Views;

/// <summary>Galería de fondos que se muestran en la pantalla de bloqueo de los equipos.</summary>
public sealed class WallpapersView : UserControl, IRefreshable
{
    private static readonly Option<WallpaperMode>[] Modes =
    {
        new(WallpaperMode.None, "Sin fondos (degradado del cliente)"),
        new(WallpaperMode.Single, "Fondo fijo"),
        new(WallpaperMode.Slideshow, "Presentación rotativa")
    };

    /// <summary>Carpeta de intercambio para administrar los fondos como archivos.</summary>
    private static readonly string ExchangeFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CyberManager", "Admin", "Fondos");

    private const string ExchangeManifest = "_sincronizacion.json";
    private const string ExchangeReadme = "_LEEME.txt";
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

    private readonly WrapPanel _gallery = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBox _searchBox = new();
    private readonly TextBlock _galleryStatus = Kit.Muted("Cargando galería...");
    private readonly ComboBox _modeCombo = Kit.Combo(Modes);
    private readonly ComboBox _singleCombo = new() { DisplayMemberPath = nameof(Option<object>.DisplayText) };
    private readonly TextBox _secondsBox = Kit.Input("30");
    private readonly TextBlock _applyStatus = Kit.Muted("Sin cambios pendientes.");
    private readonly Image _preview = new()
    {
        Height = 170,
        Stretch = Stretch.UniformToFill
    };
    private readonly TextBox _nameBox = Kit.Input();
    private readonly TextBox _orderBox = Kit.Input();
    private readonly CheckBox _activeCheck = new() { Content = "Incluir en la galería" };
    private readonly TextBlock _detailTitle = Kit.Heading("Seleccione un fondo", "H2");
    private readonly TextBlock _detailInfo = Kit.Muted("Haga clic en una imagen de la galería para editar sus datos.");
    private readonly StackPanel _detailActions = new();
    private readonly TextBlock _detailEmpty = Kit.Muted("Aún no hay fondos. Use «Subir imágenes» para agregar el primero.");

    private readonly Dictionary<int, ImageSource> _thumbnails = new();
    private readonly Dictionary<int, Border> _cards = new();
    private readonly Dictionary<int, TextBlock> _cardStatuses = new();

    private List<WallpaperDto> _wallpapers = new();
    private WallpaperPlaylistDto? _playlist;
    private int? _selectedId;
    private bool _loading;

    public WallpapersView()
    {
        BuildLayout();

        _modeCombo.SelectionChanged += (_, _) => UpdateModeState();
        _secondsBox.LostFocus += (_, _) => NormalizeSeconds();
        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudo cargar la galería");
    }

    private void BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(Kit.PageHeader("Fondos de pantalla",
            "Imágenes que verán los clientes mientras el equipo está bloqueado. Los cambios se envían al instante.",
            Kit.Button("Subir imágenes", "PrimaryButton", Upload),
            Kit.Button("Buscar en internet", "GhostButton", SearchOnInternet)));

        var body = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });

        // ---------- Galería ----------
        _searchBox.Width = 200;
        _searchBox.VerticalAlignment = VerticalAlignment.Center;
        _searchBox.ToolTip = L.T("Buscar fondo por nombre o estado");
        _searchBox.TextChanged += (_, _) => BuildGallery();

        var search = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        search.Children.Add(new TextBlock
        {
            Text = L.T("Buscar fondo"),
            Style = Kit.Style("Label"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        search.Children.Add(_searchBox);

        _galleryStatus.HorizontalAlignment = HorizontalAlignment.Right;
        _galleryStatus.VerticalAlignment = VerticalAlignment.Center;
        _galleryStatus.Margin = new Thickness(8, 0, 0, 0);

        var galleryHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        galleryHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        galleryHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        galleryHeader.Children.Add(search);
        Grid.SetColumn(_galleryStatus, 1);
        galleryHeader.Children.Add(_galleryStatus);

        var toolbarRight = Kit.HStack(8,
            Kit.Button("Reenviar a equipos", "GhostButton", RefreshClients),
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync()));
        toolbarRight.HorizontalAlignment = HorizontalAlignment.Right;
        toolbarRight.VerticalAlignment = VerticalAlignment.Center;

        var galleryToolbar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        galleryToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        galleryToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        galleryToolbar.Children.Add(Kit.HStack(8,
            Kit.Button("Abrir carpeta", "GhostButton", OpenExchangeFolder),
            Kit.Button("Sincronizar carpeta", "GhostButton", () => _ = SyncExchangeFolderAsync())));
        Grid.SetColumn(toolbarRight, 1);
        galleryToolbar.Children.Add(toolbarRight);

        var galleryContent = new Grid();
        galleryContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        galleryContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        galleryContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        galleryContent.Children.Add(galleryToolbar);
        Grid.SetRow(galleryHeader, 1);
        galleryContent.Children.Add(galleryHeader);

        var scroll = new ScrollViewer
        {
            Content = _gallery,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 2);
        galleryContent.Children.Add(scroll);

        var galleryCard = Kit.Card(galleryContent, 16);
        galleryCard.Margin = new Thickness(0, 0, 16, 0);
        body.Children.Add(galleryCard);

        // ---------- Panel de configuración ----------
        var side = new ScrollViewer
        {
            Content = BuildSidePanel(),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetColumn(side, 1);
        body.Children.Add(side);

        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Content = root;
    }

    private StackPanel BuildSidePanel()
    {
        _singleCombo.IsEnabled = false;
        _secondsBox.Width = 110;

        var applyPanel = Kit.VStack(8,
            Kit.Section("Presentación"),
            Kit.Field("Modo", _modeCombo),
            Kit.Field("Fondo fijo", _singleCombo),
            Kit.Field("Segundos entre imágenes (5 a 3600)", _secondsBox),
            Kit.Button("Guardar y aplicar", "PrimaryButton", ApplySettings),
            _applyStatus);

        var detailBody = Kit.VStack(8,
            _detailTitle,
            _detailInfo,
            new Border
            {
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(10, 18, 34)),
                Child = _preview
            },
            Kit.Field("Nombre", _nameBox),
            Kit.Field("Orden", _orderBox),
            _activeCheck,
            _detailActions,
            _detailEmpty);

        var side = Kit.VStack(14, Kit.Card(applyPanel, 16), Kit.Card(detailBody, 16));
        side.Margin = new Thickness(0, 0, 4, 4);
        UpdateModeState();
        return side;
    }

    public async Task RefreshAsync()
    {
        _loading = true;
        try
        {
            _wallpapers = await App.Api.WallpapersAsync();
            _playlist = await App.Api.WallpaperPlaylistAsync();

            if (_selectedId is not int id || _wallpapers.All(w => w.Id != id))
                _selectedId = _wallpapers.FirstOrDefault()?.Id;

            BuildGallery();
            UpdateSettingsPanel();
            UpdateDetail();
        }
        finally
        {
            _loading = false;
            UpdateModeState();
        }
    }

    private void BuildGallery()
    {
        _gallery.Children.Clear();
        _cards.Clear();
        _cardStatuses.Clear();

        if (_wallpapers.Count == 0)
        {
            _galleryStatus.Text = "Sin fondos en la galería.";
            _gallery.Children.Add(new Border
            {
                Style = Kit.Style("Panel"),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 4, 8, 8),
                Child = Kit.Muted("Suba una imagen PNG, JPG, GIF o BMP (máximo 20 MB).")
            });
            return;
        }

        var term = _searchBox.Text.Trim();
        var filtered = term.Length == 0
            ? _wallpapers
            : _wallpapers.Where(w =>
                w.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                StatusText(w).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        var active = _wallpapers.Count(w => w.Active);
        _galleryStatus.Text = term.Length == 0
            ? $"{_wallpapers.Count} fondo(s) · {active} activo(s) para la presentación."
            : $"{filtered.Count} de {_wallpapers.Count} fondo(s) · {active} activo(s).";

        if (filtered.Count == 0)
        {
            _gallery.Children.Add(new Border
            {
                Style = Kit.Style("Panel"),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 4, 8, 8),
                Child = Kit.Muted($"No hay fondos que coincidan con «{term}».")
            });
            return;
        }

        foreach (var wallpaper in filtered.OrderBy(w => w.SortOrder).ThenBy(w => w.Name))
            _gallery.Children.Add(BuildCard(wallpaper));

        UpdateSelectionVisuals();
    }

    private Border BuildCard(WallpaperDto wallpaper)
    {
        var image = new Image
        {
            Height = 124,
            Stretch = Stretch.UniformToFill,
            Source = null
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var imageFrame = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(10, 18, 34)),
            ClipToBounds = true,
            Child = image
        };

        var status = new TextBlock
        {
            Text = StatusText(wallpaper),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(7, 2, 7, 2)
        };
        var statusColor = ((SolidColorBrush)StatusBrush(wallpaper)).Color;
        var statusBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(38, statusColor.R, statusColor.G, statusColor.B)),
            Child = status
        };

        var texts = new StackPanel { Margin = new Thickness(2, 2, 2, 0) };
        texts.Children.Add(new TextBlock
        {
            Text = wallpaper.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = wallpaper.Name
        });
        texts.Children.Add(new TextBlock
        {
            Text = $"{wallpaper.Width} × {wallpaper.Height} · {SizeText(wallpaper.SizeBytes)}",
            Style = Kit.Style("Muted"),
            FontSize = 11
        });
        texts.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { statusBorder }
        });

        var body = new StackPanel();
        body.Children.Add(imageFrame);
        body.Children.Add(texts);

        var card = new Border
        {
            Style = Kit.Style("Card"),
            Width = 224,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 12, 12),
            Cursor = Cursors.Hand,
            Child = body
        };
        card.MouseLeftButtonUp += (_, _) => Select(wallpaper.Id);

        _cards[wallpaper.Id] = card;
        _cardStatuses[wallpaper.Id] = status;

        _ = LoadThumbnailAsync(wallpaper.Id, image);
        return card;
    }

    private async Task LoadThumbnailAsync(int id, Image target)
    {
        if (_thumbnails.TryGetValue(id, out var cached))
        {
            target.Source = cached;
            return;
        }

        try
        {
            var data = await App.Api.WallpaperFileAsync(id);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            // IgnoreImageCache + StreamSource lanza ArgumentNullException en WPF .NET 8.
            image.DecodePixelWidth = 520;
            image.StreamSource = new MemoryStream(data);
            image.EndInit();
            image.Freeze();

            _thumbnails[id] = image;
            target.Source = image;
        }
        catch
        {
            // La tarjeta se mantiene visible aunque la miniatura no se pueda cargar.
        }
    }

    private void UpdateSettingsPanel()
    {
        var mode = _playlist?.Mode ?? WallpaperMode.None;
        _modeCombo.SelectedIndex = Array.FindIndex(Modes, m => m.Value == mode);

        var active = _wallpapers.Where(w => w.Active)
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Name)
            .Select(w => new Option<int?>(w.Id, w.Name))
            .ToList();
        _singleCombo.ItemsSource = active;

        var activeId = _playlist?.ActiveId ?? active.FirstOrDefault()?.Value;
        _singleCombo.SelectedIndex = active.FindIndex(o => o.Value == activeId);
        if (_singleCombo.SelectedIndex < 0 && active.Count > 0) _singleCombo.SelectedIndex = 0;

        _secondsBox.Text = (_playlist?.RotationSeconds ?? WallpaperSettings.DefaultRotationSeconds)
            .ToString();

        _applyStatus.Text = mode switch
        {
            WallpaperMode.Single => "Se muestra un único fondo fijo en los equipos bloqueados.",
            WallpaperMode.Slideshow => $"Rotación activa cada {_playlist?.RotationSeconds ?? 30} segundo(s).",
            _ => "Los equipos usan el degradado original como fondo."
        };

        UpdateModeState();
    }

    private void UpdateModeState()
    {
        if (_loading) return;
        var mode = SelectedMode();
        _singleCombo.IsEnabled = mode == WallpaperMode.Single;
        _secondsBox.IsEnabled = mode == WallpaperMode.Slideshow;
        _secondsBox.Opacity = mode == WallpaperMode.Slideshow ? 1 : 0.55;
    }

    private void UpdateDetail()
    {
        var wallpaper = Current;
        _detailActions.Children.Clear();
        _detailEmpty.Visibility = wallpaper is null ? Visibility.Visible : Visibility.Collapsed;
        _preview.Visibility = wallpaper is null ? Visibility.Collapsed : Visibility.Visible;
        _nameBox.IsEnabled = _orderBox.IsEnabled = _activeCheck.IsEnabled = wallpaper is not null;

        if (wallpaper is null)
        {
            _detailTitle.Text = "Seleccione un fondo";
            _detailInfo.Text = "Haga clic en una imagen de la galería para editar sus datos.";
            _nameBox.Text = string.Empty;
            _orderBox.Text = string.Empty;
            _activeCheck.IsChecked = false;
            _preview.Source = null;
            return;
        }

        _detailTitle.Text = wallpaper.Name;
        _detailInfo.Text = $"{wallpaper.Width} × {wallpaper.Height} · {SizeText(wallpaper.SizeBytes)} · " +
                           $"{wallpaper.CreatedUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
        _nameBox.Text = wallpaper.Name;
        _orderBox.Text = wallpaper.SortOrder.ToString();
        _activeCheck.IsChecked = wallpaper.Active;

        if (_thumbnails.TryGetValue(wallpaper.Id, out var image))
            _preview.Source = image;
        else
            _ = LoadPreviewAsync(wallpaper.Id);

        _detailActions.Children.Add(Kit.HStack(8,
            Kit.Button("Guardar cambios", "PrimaryButton", SaveDetail),
            Kit.Button("Eliminar", "DangerButton", DeleteCurrent)));
    }

    private async Task LoadPreviewAsync(int id)
    {
        try
        {
            var data = await App.Api.WallpaperFileAsync(id);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            // IgnoreImageCache + StreamSource lanza ArgumentNullException en WPF .NET 8.
            image.DecodePixelWidth = 760;
            image.StreamSource = new MemoryStream(data);
            image.EndInit();
            image.Freeze();

            _thumbnails[id] = image;
            if (_selectedId == id) _preview.Source = image;
        }
        catch
        {
            // Sin vista previa: los datos del fondo siguen disponibles.
        }
    }

    private void Select(int id)
    {
        _selectedId = id;
        UpdateSelectionVisuals();
        UpdateDetail();
    }

    private void UpdateSelectionVisuals()
    {
        var accent = Kit.Brush("AccentBrush");
        var border = Kit.Brush("BorderBrush");

        foreach (var (id, card) in _cards)
        {
            var selected = id == _selectedId;
            card.BorderBrush = selected ? accent : border;
            card.BorderThickness = new Thickness(selected ? 2 : 1);
        }

        foreach (var (id, status) in _cardStatuses)
        {
            status.Text = StatusText(_wallpapers.First(w => w.Id == id));
            status.Foreground = StatusBrush(_wallpapers.First(w => w.Id == id));
        }
    }

    private void Upload()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccione imágenes para la galería",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Todos los archivos|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;
        _ = UploadFilesAsync(dialog.FileNames);
    }

    /// <summary>Abre el buscador de fondos en internet y refresca la galería si se agregó alguno.</summary>
    private void SearchOnInternet()
    {
        var window = new InternetWallpapersWindow();
        var owner = Window.GetWindow(this);
        if (owner is not null && !ReferenceEquals(owner, window))
            window.Owner = owner;

        window.ShowDialog();

        if (window.AddedCount > 0)
            _ = Ui.Run(RefreshAsync, "No se pudo actualizar la galería");
    }

    private async Task UploadFilesAsync(IEnumerable<string> files)
    {
        var list = files.ToList();
        if (list.Count == 0) return;

        var errors = new List<string>();
        var uploaded = 0;

        foreach (var file in list)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(file);
                await App.Api.UploadWallpaperAsync(Path.GetFileName(file), bytes, ContentType(file),
                    Path.GetFileNameWithoutExtension(file));
                uploaded++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        await Ui.Run(RefreshAsync, "No se pudo actualizar la galería");

        if (errors.Count > 0)
            Ui.Error(string.Join("\n", errors), $"No se subieron {errors.Count} imagen(es)");
        else if (uploaded > 0)
            Ui.Info($"{uploaded} fondo(s) agregados a la galería.", "Galería actualizada");
    }

    // ============================================================
    //  Carpeta de intercambio
    // ============================================================

    /// <summary>Exporta la galería a una carpeta local y la abre en el Explorador.</summary>
    private async void OpenExchangeFolder()
    {
        await Ui.Run(async () =>
        {
            Directory.CreateDirectory(ExchangeFolder);
            await ExportToExchangeFolderAsync();
            Process.Start(new ProcessStartInfo { FileName = ExchangeFolder, UseShellExecute = true });
        }, "No se pudo abrir la carpeta de fondos");
    }

    /// <summary>Copia a la carpeta los fondos que aún no tienen archivo y actualiza el manifiesto.</summary>
    private async Task ExportToExchangeFolderAsync()
    {
        Directory.CreateDirectory(ExchangeFolder);

        var entries = LoadExchangeEntries();

        // Los fondos eliminados desde la consola también se quitan de la carpeta.
        foreach (var stale in entries.Values.Where(e => _wallpapers.All(w => w.Id != e.Id)).ToList())
        {
            var stalePath = Path.Combine(ExchangeFolder, stale.File);
            if (File.Exists(stalePath)) File.Delete(stalePath);
            entries.Remove(stale.Id);
        }

        var usedNames = new HashSet<string>(
            entries.Values.Select(e => e.File), StringComparer.OrdinalIgnoreCase);

        foreach (var wallpaper in _wallpapers.OrderBy(w => w.SortOrder).ThenBy(w => w.Name))
        {
            if (entries.TryGetValue(wallpaper.Id, out var entry) &&
                File.Exists(Path.Combine(ExchangeFolder, entry.File)))
                continue;

            var fileName = BuildExchangeFileName(wallpaper, usedNames);
            var path = Path.Combine(ExchangeFolder, fileName);
            var data = await App.Api.WallpaperFileAsync(wallpaper.Id);
            await File.WriteAllBytesAsync(path, data);

            entries[wallpaper.Id] = new ExchangeEntry
            {
                Id = wallpaper.Id,
                File = fileName,
                Sha256 = wallpaper.Sha256
            };
            usedNames.Add(fileName);
        }

        SaveExchangeEntries(entries);
        WriteExchangeReadme();
    }

    /// <summary>
    /// Importa las imágenes nuevas y reemplaza las que cambiaron en la carpeta de intercambio.
    /// La comparación se hace con el hash guardado en el manifiesto.
    /// </summary>
    private async Task SyncExchangeFolderAsync()
    {
        if (!Directory.Exists(ExchangeFolder))
        {
            Ui.Info("La carpeta todavía no existe. Use «Abrir carpeta» para crearla.", "Carpeta de fondos");
            return;
        }

        var entries = LoadExchangeEntries();
        var byFile = entries.Values
            .GroupBy(e => e.File, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(ExchangeFolder)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var added = 0;
        var updated = 0;
        var errors = new List<string>();
        var nextOrder = (_wallpapers.Count == 0 ? 0 : _wallpapers.Max(w => w.SortOrder)) + 1;

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            try
            {
                var data = await File.ReadAllBytesAsync(file);
                if (data.Length == 0) continue;
                if (data.Length > ImageInspector.MaxBytes)
                {
                    errors.Add($"{fileName}: supera el máximo de {ImageInspector.MaxBytes / 1024 / 1024} MB.");
                    continue;
                }

                var sha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                var contentType = ContentType(fileName);

                if (byFile.TryGetValue(fileName, out var entry) &&
                    _wallpapers.FirstOrDefault(w => w.Id == entry.Id) is { } current)
                {
                    if (string.Equals(entry.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                        continue; // Sin cambios desde la última exportación.

                    await App.Api.UpdateWallpaperFileAsync(entry.Id, fileName, data, contentType,
                        current.Name, current.SortOrder, current.Active);
                    entry.Sha256 = sha;
                    updated++;
                }
                else
                {
                    if (entry is not null) entries.Remove(entry.Id); // Referencia a un fondo eliminado.

                    var (name, order) = ParseExchangeFileName(fileName, nextOrder);
                    var created = await App.Api.UploadWallpaperAsync(fileName, data, contentType, name, order);

                    var createdEntry = new ExchangeEntry
                    {
                        Id = created.Id,
                        File = fileName,
                        Sha256 = created.Sha256
                    };
                    entries[created.Id] = createdEntry;
                    byFile[fileName] = createdEntry;
                    nextOrder = Math.Max(nextOrder, order + 1);
                    added++;
                }

                SaveExchangeEntries(entries);
            }
            catch (Exception ex)
            {
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        await RefreshAsync();

        if (errors.Count > 0)
            Ui.Error(string.Join("\n", errors), "Sincronización incompleta");
        else if (added == 0 && updated == 0)
            Ui.Info("No hay imágenes nuevas ni modificadas en la carpeta.", "Carpeta de fondos");
        else
            Ui.Info($"{added} fondo(s) agregados y {updated} actualizado(s).", "Carpeta de fondos");
    }

    private static Dictionary<int, ExchangeEntry> LoadExchangeEntries()
    {
        var path = Path.Combine(ExchangeFolder, ExchangeManifest);
        try
        {
            if (!File.Exists(path)) return new();

            var list = JsonSerializer.Deserialize<List<ExchangeEntry>>(File.ReadAllText(path));
            return (list ?? new List<ExchangeEntry>())
                .Where(e => e.Id > 0 && !string.IsNullOrWhiteSpace(e.File))
                .GroupBy(e => e.Id)
                .ToDictionary(g => g.Key, g => g.First());
        }
        catch
        {
            return new();
        }
    }

    private static void SaveExchangeEntries(Dictionary<int, ExchangeEntry> entries)
    {
        var path = Path.Combine(ExchangeFolder, ExchangeManifest);
        var json = JsonSerializer.Serialize(entries.Values.OrderBy(e => e.Id).ToList(),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(true));
    }

    private static void WriteExchangeReadme()
    {
        var lines = new[]
        {
            "CyberManager · Carpeta de fondos",
            string.Empty,
            "Esta carpeta sincroniza la galería de fondos con el servidor.",
            string.Empty,
            "· Para agregar un fondo, copie una imagen PNG, JPG, GIF o BMP aquí",
            "  y pulse «Sincronizar carpeta» en la consola.",
            "· Para reemplazar un fondo, edite su archivo y guárdelo con el mismo nombre.",
            "  Al sincronizar, la galería se actualiza con la nueva imagen.",
            "· El número al inicio del nombre indica el orden (001, 002, ...).",
            "· Para eliminar un fondo, use el botón «Eliminar» de la consola.",
            "· Las imágenes pueden pesar hasta 20 MB.",
            string.Empty,
            "El archivo _sincronizacion.json lo usa la consola para detectar cambios; no lo edite."
        };

        File.WriteAllText(Path.Combine(ExchangeFolder, ExchangeReadme),
            string.Join(Environment.NewLine, lines), new UTF8Encoding(true));
    }

    private static string BuildExchangeFileName(WallpaperDto wallpaper, HashSet<string> used)
    {
        var name = SanitizeFileName(wallpaper.Name);
        if (name.Length == 0) name = $"Fondo {wallpaper.Id}";

        var baseName = $"{wallpaper.SortOrder:000} - {name}";
        var candidate = baseName + wallpaper.Extension;
        var copy = 2;
        while (used.Contains(candidate))
            candidate = $"{baseName} ({copy++}){wallpaper.Extension}";

        return candidate;
    }

    private static (string Name, int SortOrder) ParseExchangeFileName(string fileName, int fallbackOrder)
    {
        var bare = Path.GetFileNameWithoutExtension(fileName).Trim();
        var match = Regex.Match(bare, @"^(\d{1,4})\s*[-–]\s*(.+)$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var order))
            return (match.Groups[2].Value.Trim(), order);

        return (bare.Length == 0 ? "Fondo" : bare, fallbackOrder);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray())
            .Trim()
            .TrimEnd('.');
        return cleaned.Length > 80 ? cleaned[..80].TrimEnd() : cleaned;
    }

    private sealed class ExchangeEntry
    {
        public int Id { get; set; }
        public string File { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    private async void ApplySettings()
    {
        if (_loading) return;

        var mode = SelectedMode();
        var seconds = ParseSeconds();
        if (seconds is null) return;

        int? activeId = null;
        if (mode == WallpaperMode.Single)
        {
            if (_singleCombo.SelectedItem is not Option<int?> option)
            {
                Ui.Error("Seleccione el fondo que se mostrará de forma fija.");
                return;
            }
            activeId = option.Value;
        }

        await Ui.Run(async () =>
        {
            _playlist = await App.Api.ApplyWallpaperAsync(
                new ApplyWallpaperSettingsRequest(mode, activeId, seconds.Value));
            await RefreshAsync();
            Ui.Info($"Presentación actualizada: {WallpaperSettings.ModeText(mode)}.", "Fondos de pantalla");
        }, "No se pudo aplicar la presentación");
    }

    private async void RefreshClients()
    {
        await Ui.Run(async () =>
        {
            _playlist = await App.Api.RefreshWallpapersAsync();
            await RefreshAsync();
            Ui.Info("La galería se reenvió a todos los equipos conectados.", "Fondos de pantalla");
        }, "No se pudo reenviar la galería");
    }

    private async void SaveDetail()
    {
        if (Current is not { } wallpaper) return;

        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Ui.Error("Indique un nombre para el fondo.");
            return;
        }

        var order = int.TryParse(_orderBox.Text.Trim(), out var parsedOrder)
            ? parsedOrder
            : wallpaper.SortOrder;

        await Ui.Run(async () =>
        {
            await App.Api.UpdateWallpaperAsync(wallpaper.Id,
                new SaveWallpaperRequest(name, _activeCheck.IsChecked == true, order));
            await RefreshAsync();
        }, "No se pudo guardar el fondo");
    }

    private async void DeleteCurrent()
    {
        if (Current is not { } wallpaper) return;
        if (!Ui.Confirm($"¿Eliminar el fondo «{wallpaper.Name}» de la galería?\n\n" +
                        "Los equipos dejarán de mostrarlo inmediatamente.", "Eliminar fondo")) return;

        await Ui.Run(async () =>
        {
            await App.Api.DeleteWallpaperAsync(wallpaper.Id);
            _thumbnails.Remove(wallpaper.Id);
            _selectedId = null;
            await RefreshAsync();
        }, "No se pudo eliminar el fondo");
    }

    private WallpaperDto? Current
        => _selectedId is int id ? _wallpapers.FirstOrDefault(w => w.Id == id) : null;

    private WallpaperMode SelectedMode()
        => _modeCombo.SelectedItem is Option<WallpaperMode> option ? option.Value : WallpaperMode.None;

    private int? ParseSeconds()
    {
        if (!int.TryParse(_secondsBox.Text.Trim(), out var seconds))
        {
            Ui.Error($"Indique un número de segundos entre {WallpaperSettings.MinRotationSeconds} y " +
                     $"{WallpaperSettings.MaxRotationSeconds}.");
            return null;
        }

        if (seconds < WallpaperSettings.MinRotationSeconds || seconds > WallpaperSettings.MaxRotationSeconds)
        {
            Ui.Error($"La rotación debe estar entre {WallpaperSettings.MinRotationSeconds} y " +
                     $"{WallpaperSettings.MaxRotationSeconds} segundos.");
            return null;
        }

        _secondsBox.Text = seconds.ToString();
        return seconds;
    }

    private void NormalizeSeconds()
    {
        if (int.TryParse(_secondsBox.Text.Trim(), out var seconds))
            _secondsBox.Text = Math.Clamp(seconds, WallpaperSettings.MinRotationSeconds,
                WallpaperSettings.MaxRotationSeconds).ToString();
        else
            _secondsBox.Text = WallpaperSettings.DefaultRotationSeconds.ToString();
    }

    private string StatusText(WallpaperDto wallpaper)
    {
        if (!wallpaper.Active) return "OCULTO";
        if (_playlist?.Mode == WallpaperMode.Single && _playlist.ActiveId == wallpaper.Id) return "FONDO FIJO";
        if (_playlist?.Mode == WallpaperMode.Slideshow) return "EN ROTACIÓN";
        return "DISPONIBLE";
    }

    private static Brush StatusBrush(WallpaperDto wallpaper)
    {
        if (!wallpaper.Active) return Kit.Brush("MutedBrush");
        return Kit.Brush("SuccessBrush");
    }

    private static string SizeText(long bytes)
        => bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:N1} MB"
            : $"{Math.Max(1, bytes / 1024d):N0} KB";

    private static string ContentType(string file)
        => Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
}

