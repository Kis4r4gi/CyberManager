using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.Win32;

namespace CyberManager.Admin.Views;

/// <summary>Videos de promoción que se reproducen en los terminales bloqueados.</summary>
public sealed class PromosView : UserControl, IRefreshable
{
    private readonly WrapPanel _gallery = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _galleryStatus = Kit.Muted("Cargando videos...");
    private readonly TextBlock _detailTitle = Kit.Heading("Seleccione un video", "H2");
    private readonly TextBlock _detailInfo = Kit.Muted("Haga clic en un video de la galería para editar sus datos.");
    private readonly TextBlock _detailEmpty = Kit.Muted("Aún no hay videos. Use «Subir videos» para agregar el primero.");
    private readonly TextBlock _applyStatus = Kit.Muted("Los terminales reciben los cambios automáticamente.");
    private readonly TextBox _nameBox = Kit.Input();
    private readonly TextBox _orderBox = Kit.Input();
    private readonly CheckBox _activeCheck = new() { Content = "Reproducir en terminales bloqueados" };
    private readonly StackPanel _detailActions = new();
    private readonly MediaElement _preview = new()
    {
        Height = 178,
        LoadedBehavior = MediaState.Manual,
        UnloadedBehavior = MediaState.Manual,
        Volume = 0.65,
        Visibility = Visibility.Collapsed
    };

    private readonly Dictionary<int, Border> _cards = new();
    private readonly Dictionary<int, TextBlock> _cardStatuses = new();

    private List<PromoVideoDto> _videos = new();
    private PromoVideoPlaylistDto? _playlist;
    private int? _selectedId;
    private string? _previewFile;

    public PromosView()
    {
        BuildLayout();
        Loaded += async (_, _) =>
        {
            Loc.Apply(this);
            await Ui.Run(RefreshAsync, "No se pudo cargar la galería de videos");
        };
        Unloaded += (_, _) => StopPreview();
    }

    private void BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(Kit.PageHeader("Videos promocionales",
            "Videos que verán los clientes mientras el equipo está bloqueado. Se sincronizan con todos los terminales.",
            Kit.Button("Subir videos", "PrimaryButton", Upload),
            Kit.Button("Reenviar a terminales", "GhostButton", RefreshClients),
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync())));

        var body = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });

        var galleryContent = new Grid();
        galleryContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        galleryContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var galleryHeader = Kit.HStack(12, _galleryStatus);
        galleryHeader.Margin = new Thickness(0, 0, 0, 6);
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
        var previewFrame = new Border
        {
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.FromRgb(10, 18, 34)),
            Child = _preview,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var detailBody = Kit.VStack(8,
            _detailTitle,
            _detailInfo,
            previewFrame,
            Kit.Field("Nombre", _nameBox),
            Kit.Field("Orden", _orderBox),
            _activeCheck,
            _detailActions,
            _detailEmpty);

        _applyStatus.TextWrapping = TextWrapping.Wrap;
        var side = Kit.VStack(14, Kit.Card(detailBody, 16), Kit.Card(_applyStatus, 14));
        side.Margin = new Thickness(0, 0, 4, 4);
        return side;
    }

    public async Task RefreshAsync()
    {
        _videos = await App.Api.PromoVideosAsync();
        _playlist = await App.Api.PromoVideoPlaylistAsync();

        if (_selectedId is not int id || _videos.All(v => v.Id != id))
            _selectedId = _videos.FirstOrDefault()?.Id;

        BuildGallery();
        UpdateDetail();
    }

    private void BuildGallery()
    {
        _gallery.Children.Clear();
        _cards.Clear();
        _cardStatuses.Clear();

        if (_videos.Count == 0)
        {
            _galleryStatus.Text = "Sin videos en la galería.";
            _gallery.Children.Add(new Border
            {
                Style = Kit.Style("Panel"),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 4, 8, 8),
                Child = Kit.Muted("Suba un video MP4, M4V, WMV o AVI (máximo 300 MB).")
            });
            return;
        }

        var active = _videos.Count(v => v.Active);
        _galleryStatus.Text = $"{_videos.Count} video(s) · {active} activo(s) para los terminales.";

        foreach (var video in _videos.OrderBy(v => v.SortOrder).ThenBy(v => v.Name))
            _gallery.Children.Add(BuildCard(video));

        UpdateSelectionVisuals();
    }

    private Border BuildCard(PromoVideoDto video)
    {
        var icon = new Border
        {
            Height = 104,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(12, 23, 42)),
            Child = new TextBlock
            {
                Text = "\uE714",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 42,
                Foreground = Kit.Brush("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var status = new TextBlock
        {
            Text = StatusText(video),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(7, 2, 7, 2)
        };
        var statusColor = (video.Active ? Kit.Brush("SuccessBrush") : Kit.Brush("MutedBrush")) is SolidColorBrush brush
            ? brush.Color
            : Colors.Gray;
        var statusBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(38, statusColor.R, statusColor.G, statusColor.B)),
            Child = status
        };

        var texts = new StackPanel { Margin = new Thickness(2, 2, 2, 0) };
        texts.Children.Add(new TextBlock
        {
            Text = video.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = video.Name
        });
        texts.Children.Add(new TextBlock
        {
            Text = $"{video.Extension.TrimStart('.').ToUpperInvariant()} · {SizeText(video.SizeBytes)}",
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
        body.Children.Add(icon);
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
        card.MouseLeftButtonUp += (_, _) => Select(video.Id);

        _cards[video.Id] = card;
        _cardStatuses[video.Id] = status;
        return card;
    }

    private void UpdateDetail()
    {
        var video = Current;
        _detailActions.Children.Clear();
        _detailEmpty.Visibility = video is null ? Visibility.Visible : Visibility.Collapsed;
        _nameBox.IsEnabled = _orderBox.IsEnabled = _activeCheck.IsEnabled = video is not null;

        if (video is null)
        {
            _detailTitle.Text = "Seleccione un video";
            _detailInfo.Text = "Haga clic en un video de la galería para editar sus datos.";
            _nameBox.Text = string.Empty;
            _orderBox.Text = string.Empty;
            _activeCheck.IsChecked = false;
            StopPreview();
            return;
        }

        _detailTitle.Text = video.Name;
        _detailInfo.Text = $"{video.Extension.TrimStart('.').ToUpperInvariant()} · {SizeText(video.SizeBytes)} · " +
                           $"{video.CreatedUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
        _nameBox.Text = video.Name;
        _orderBox.Text = video.SortOrder.ToString();
        _activeCheck.IsChecked = video.Active;

        _detailActions.Children.Add(Kit.HStack(8,
            Kit.Button("Guardar cambios", "PrimaryButton", SaveDetail),
            Kit.Button("Eliminar", "DangerButton", DeleteCurrent)));

        _ = LoadPreviewAsync(video.Id, video.Extension);
    }

    private async Task LoadPreviewAsync(int id, string extension)
    {
        try
        {
            var data = await App.Api.GetBytesAsync($"/api/promo-videos/{id}/file");
            if (data.Length == 0) return;

            var file = Path.Combine(Path.GetTempPath(),
                $"cybermanager-promo-{id}{extension}");
            await File.WriteAllBytesAsync(file, data);
            _previewFile = file;

            if (_selectedId != id) return;
            _preview.Source = new Uri(file, UriKind.Absolute);
            _preview.Visibility = Visibility.Visible;
            _preview.Play();
        }
        catch
        {
            _preview.Visibility = Visibility.Collapsed;
        }
    }

    private void Select(int id)
    {
        if (_selectedId == id) return;
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
            var video = _videos.First(v => v.Id == id);
            status.Text = StatusText(video);
            status.Foreground = video.Active ? Kit.Brush("SuccessBrush") : Kit.Brush("MutedBrush");
        }
    }

    private void Upload()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccione videos para los terminales",
            Filter = "Videos|*.mp4;*.m4v;*.wmv;*.avi|Todos los archivos|*.*",
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
                var info = new FileInfo(file);
                if (info.Length > PromoSettings.MaxBytes)
                {
                    errors.Add($"{info.Name}: supera el máximo de 300 MB.");
                    continue;
                }

                if (!PromoSettings.IsSupported(file))
                {
                    errors.Add($"{info.Name}: formato no compatible.");
                    continue;
                }

                var bytes = await File.ReadAllBytesAsync(file);
                await App.Api.UploadPromoVideoAsync(info.Name, bytes,
                    PromoSettings.ContentType(info.Extension),
                    Path.GetFileNameWithoutExtension(info.Name));
                uploaded++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        await Ui.Run(RefreshAsync, "No se pudo actualizar la galería de videos");

        if (errors.Count > 0)
            Ui.Error(string.Join("\n", errors), $"No se subieron {errors.Count} video(s)");
        else if (uploaded > 0)
            Ui.Info($"{uploaded} video(s) agregados a la galería.", "Videos promocionales");
    }

    private async void RefreshClients()
    {
        await Ui.Run(async () =>
        {
            _playlist = await App.Api.RefreshPromoVideosAsync();
            await RefreshAsync();
            Ui.Info("La lista de videos se reenvió a todos los terminales conectados.",
                "Videos promocionales");
        }, "No se pudo reenviar la lista de videos");
    }

    private async void SaveDetail()
    {
        if (Current is not { } video) return;

        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Ui.Error("Indique un nombre para el video.");
            return;
        }

        var order = int.TryParse(_orderBox.Text.Trim(), out var parsedOrder)
            ? parsedOrder
            : video.SortOrder;

        await Ui.Run(async () =>
        {
            await App.Api.UpdatePromoVideoAsync(video.Id,
                new SavePromoVideoRequest(name, _activeCheck.IsChecked == true, order));
            await RefreshAsync();
        }, "No se pudo guardar el video");
    }

    private async void DeleteCurrent()
    {
        if (Current is not { } video) return;
        if (!Ui.Confirm($"¿Eliminar el video «{video.Name}» de la galería?\n\n" +
                        "Los terminales dejarán de reproducirlo inmediatamente.", "Eliminar video")) return;

        await Ui.Run(async () =>
        {
            await App.Api.DeletePromoVideoAsync(video.Id);
            _selectedId = null;
            await RefreshAsync();
        }, "No se pudo eliminar el video");
    }

    private void StopPreview()
    {
        try { _preview.Stop(); } catch { /* sin fuente */ }
        _preview.Source = null;
        _preview.Visibility = Visibility.Collapsed;

        if (_previewFile is not null)
        {
            try { File.Delete(_previewFile); } catch { /* archivo en uso */ }
        }
        _previewFile = null;
    }

    private PromoVideoDto? Current
        => _selectedId is int id ? _videos.FirstOrDefault(v => v.Id == id) : null;

    private string StatusText(PromoVideoDto video)
        => video.Active ? "EN TERMINALES" : "OCULTO";

    private static string SizeText(long bytes)
        => bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:N1} MB"
            : $"{Math.Max(1, bytes / 1024d):N0} KB";
}
