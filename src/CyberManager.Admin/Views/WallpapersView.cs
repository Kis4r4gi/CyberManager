using System.IO;
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

    private readonly WrapPanel _gallery = new() { Margin = new Thickness(0, 4, 0, 0) };
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
            Kit.Button("Reenviar a equipos", "GhostButton", RefreshClients),
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync())));

        var body = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });

        // ---------- Galería ----------
        var galleryHeader = Kit.HStack(12, _galleryStatus);
        galleryHeader.Margin = new Thickness(0, 0, 0, 6);

        var galleryContent = new Grid();
        galleryContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        galleryContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        galleryContent.Children.Add(galleryHeader);

        var scroll = new ScrollViewer
        {
            Content = _gallery,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
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
            Kit.HStack(8,
                Kit.Button("Guardar y aplicar", "PrimaryButton", ApplySettings),
                Kit.Button("Vista previa", "GhostButton", RefreshClients)),
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

        var active = _wallpapers.Count(w => w.Active);
        _galleryStatus.Text = $"{_wallpapers.Count} fondo(s) · {active} activo(s) para la presentación.";

        foreach (var wallpaper in _wallpapers.OrderBy(w => w.SortOrder).ThenBy(w => w.Name))
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
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
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
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
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

