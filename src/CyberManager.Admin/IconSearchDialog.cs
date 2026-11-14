using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Admin.Services;
using CyberManager.Admin.Views;

namespace CyberManager.Admin;

/// <summary>
/// Busca imágenes en internet (Openverse, solo CC0 o dominio público) y devuelve la
/// elegida como PNG de hasta 512 px. Se abre desde el formulario del producto y desde
/// su menú desplegable.
/// </summary>
public sealed class IconSearchDialog : FormDialog
{
    private const int MaxResultSize = 512;

    private readonly TextBox _query;
    private readonly Button _searchButton;
    private readonly TextBlock _status;
    private readonly UniformGrid _tiles;
    private readonly CancellationTokenSource _dialogCts = new();
    private readonly bool _autoSearch;
    private CancellationTokenSource? _thumbCts;
    private Border? _selectedTile;
    private WebImageResult? _selected;
    private bool _searching;
    private bool _started;
    private bool _closed;

    /// <summary>Imagen elegida, ya convertida a PNG.</summary>
    public byte[]? Result { get; private set; }

    public IconSearchDialog(string productName, string? initialQuery = null)
        : base("Buscar imagen en internet",
            $"Elija una imagen para {productName}. Se buscan imágenes CC0 o de dominio público y se guardan en PNG.",
            "Usar imagen", 820)
    {
        SizeToContent = SizeToContent.Manual;
        Height = 650;

        _autoSearch = !string.IsNullOrWhiteSpace(initialQuery);
        _query = new TextBox
        {
            Text = _autoSearch ? initialQuery!.Trim() : productName,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _query.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            _ = SearchAsync();
        };

        _searchButton = Kit.Button("Buscar", "PrimaryButton", () => _ = SearchAsync());

        var searchRow = new Grid();
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.Children.Add(_query);
        Grid.SetColumn(_searchButton, 1);
        searchRow.Children.Add(_searchButton);
        Add(searchRow, "Buscar imagen (por ejemplo: hamburguesa, refresco, café, agua)");

        _status = new TextBlock
        {
            Text = "Escriba un término y pulse Buscar.",
            Foreground = Kit.Brush("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11.5,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Body.Children.Add(_status);

        _tiles = new UniformGrid { Columns = 4 };
        Body.Children.Add(new ScrollViewer
        {
            Content = _tiles,
            Height = 300,
            Margin = new Thickness(0, 6, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });

        AddNote("Las imágenes provienen de Openverse y son CC0 o de dominio público: puede usarlas sin " +
                $"atribución. Se guardan como PNG de hasta {MaxResultSize} px.");

        Loaded += (_, _) =>
        {
            if (_started) return;
            _started = true;
            if (!_autoSearch || string.IsNullOrWhiteSpace(_query.Text))
                SetStatus("Escriba qué imagen desea buscar y pulse Buscar.");
            else
                _ = SearchAsync();
        };

        Closed += (_, _) =>
        {
            _closed = true;
            _dialogCts.Cancel();
            _thumbCts?.Cancel();
        };
    }

    private async Task SearchAsync()
    {
        if (_searching) return;

        _searching = true;
        _searchButton.IsEnabled = false;
        _thumbCts?.Cancel();
        _thumbCts?.Dispose();
        _thumbCts = CancellationTokenSource.CreateLinkedTokenSource(_dialogCts.Token);
        var thumbToken = _thumbCts.Token;

        _tiles.Children.Clear();
        _selected = null;
        _selectedTile = null;
        SetStatus("Buscando imágenes…");

        try
        {
            var results = await WebImageSearch.SearchAsync(_query.Text, 20, _dialogCts.Token);
            if (_closed) return;

            if (results.Count == 0)
            {
                SetStatus("No se encontraron imágenes. Pruebe con otra palabra.", "WarningBrush");
                return;
            }

            var loads = new List<ThumbnailLoad>(results.Count);
            foreach (var result in results)
            {
                var tile = CreateTile(result, out var load);
                loads.Add(load);
                _tiles.Children.Add(tile);
            }

            SetStatus($"{results.Count} imágenes encontradas. Haga clic en una para elegirla.");
            _ = LoadThumbnailsAsync(loads, thumbToken);
        }
        catch (OperationCanceledException)
        {
            // El diálogo se cerró o se lanzó otra búsqueda.
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, "DangerBrush");
        }
        finally
        {
            _searching = false;
            _searchButton.IsEnabled = true;
        }
    }

    private Border CreateTile(WebImageResult result, out ThumbnailLoad load)
    {
        var image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(2) };
        var placeholder = new TextBlock
        {
            Text = "Cargando…",
            Foreground = Kit.Brush("MutedBrush"),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var imageArea = new Grid { Height = 82 };
        imageArea.Children.Add(placeholder);
        imageArea.Children.Add(image);

        var title = new TextBlock
        {
            Text = result.Title,
            Foreground = Kit.Brush("TextBrush"),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 28,
            Margin = new Thickness(0, 3, 0, 0)
        };

        var license = new TextBlock
        {
            Text = result.LicenseLabel,
            Foreground = Kit.Brush("MutedBrush"),
            FontSize = 9.5
        };

        var stack = new StackPanel();
        stack.Children.Add(imageArea);
        stack.Children.Add(title);
        stack.Children.Add(license);

        var tile = new Border
        {
            Height = 142,
            Margin = new Thickness(4),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(8),
            Background = Kit.Brush("SurfaceBrush"),
            BorderBrush = Kit.Brush("BorderBrush"),
            BorderThickness = new Thickness(2),
            Cursor = Cursors.Hand,
            Child = stack,
            Tag = result,
            ToolTip = $"{result.Description}\n{result.LicenseLabel} · {result.Width}×{result.Height} px"
        };

        tile.MouseLeftButtonUp += (_, _) => Select(tile, result);
        tile.MouseEnter += (_, _) =>
        {
            if (!ReferenceEquals(tile, _selectedTile)) tile.Background = Kit.Brush("SurfaceAltBrush");
        };
        tile.MouseLeave += (_, _) =>
        {
            if (!ReferenceEquals(tile, _selectedTile)) tile.Background = Kit.Brush("SurfaceBrush");
        };

        load = new ThumbnailLoad(tile, image, placeholder, result);
        return tile;
    }

    private void Select(Border tile, WebImageResult result)
    {
        if (_selectedTile is not null)
        {
            _selectedTile.BorderBrush = Kit.Brush("BorderBrush");
            _selectedTile.Background = Kit.Brush("SurfaceBrush");
        }

        _selectedTile = tile;
        _selected = result;
        tile.BorderBrush = Kit.Brush("AccentBrush");
        tile.Background = Kit.Brush("SelectionBrush");

        SetStatus($"Seleccionada: {result.Title} · {result.LicenseLabel}. Pulse «Usar imagen» para guardarla.");
    }

    private async Task LoadThumbnailsAsync(List<ThumbnailLoad> loads, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = loads.Select(async load =>
        {
            var entered = false;
            try
            {
                await gate.WaitAsync(ct);
                entered = true;

                var data = await WebImageSearch.TryDownloadThumbnailAsync(load.Result, ct);
                if (data is null)
                {
                    await Dispatcher.InvokeAsync(() => load.Placeholder.Text = "Sin vista previa");
                    return;
                }

                var source = ProductImage.Decode(data);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_closed) return;
                    load.Image.Source = source;
                    load.Placeholder.Visibility = Visibility.Collapsed;
                });
            }
            catch (OperationCanceledException)
            {
                // Se cerró el diálogo o se lanzó otra búsqueda.
            }
            catch
            {
                try { await Dispatcher.InvokeAsync(() => load.Placeholder.Text = "Sin vista previa"); }
                catch { /* la ventana ya se cerró */ }
            }
            finally
            {
                if (entered) gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    protected override void OnAccept()
    {
        if (_selected is not WebImageResult result)
            throw new InvalidOperationException("Seleccione una imagen de la lista.");

        _thumbCts?.Cancel();
        AcceptButton.IsEnabled = false;
        SetStatus("Descargando y preparando la imagen…");
        _ = SaveAsync(result);
    }

    private async Task SaveAsync(WebImageResult result)
    {
        try
        {
            var png = await WebImageSearch.DownloadPngAsync(result, MaxResultSize, _dialogCts.Token);
            Result = png;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            // El usuario cerró la ventana mientras se descargaba.
        }
        catch (Exception ex)
        {
            if (_closed) return;
            SetStatus(ex.Message, "DangerBrush");
            AcceptButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text, string brushKey = "MutedBrush")
    {
        _status.Text = text;
        _status.Foreground = Kit.Brush(brushKey);
    }

    private sealed record ThumbnailLoad(Border Tile, Image Image, TextBlock Placeholder,
        WebImageResult Result);
}
