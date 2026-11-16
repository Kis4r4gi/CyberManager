using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CyberManager.Admin.Services;
using CyberManager.Shared;

namespace CyberManager.Admin.Views;

/// <summary>
/// Buscador de fondos en Wallhaven. La imagen elegida se descarga y se agrega a la
/// galería interna del servidor mediante la API existente de fondos de pantalla.
/// </summary>
public sealed class InternetWallpapersWindow : Window
{
    private const string ApiBase = "https://wallhaven.cc/api/v1/search";

    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly (string Label, int Value)[] Categories =
    {
        ("General", 100),
        ("Anime", 10),
        ("Personas", 1),
        ("Todas", 111)
    };

    private static readonly (string Label, int Width, int Height)[] Resolutions =
    {
        ("1920 × 1080 o más", 1920, 1080),
        ("2560 × 1440 o más", 2560, 1440),
        ("3840 × 2160 o más", 3840, 2160)
    };

    private readonly TextBox _searchBox = new();
    private readonly ComboBox _categoryBox = new();
    private readonly ComboBox _resolutionBox = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _emptyText = new();
    private readonly WrapPanel _results = new();
    private readonly ScrollViewer _scroll;
    private readonly Button _moreButton;

    private int _page;
    private int _lastPage = 1;
    private string _term = "gaming";
    private bool _searching;
    private int _added;

    public InternetWallpapersWindow()
    {
        Title = "Buscar fondos en internet";
        Width = 1200;
        Height = 840;
        MinWidth = 920;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("BgBrush");

        // ------------------------- Barra de búsqueda -------------------------
        _searchBox.Width = 300;
        _searchBox.Text = _term;
        _searchBox.Margin = new Thickness(0, 0, 10, 0);
        _searchBox.ToolTip = "Ej.: cyberpunk, gaming, anime, naturaleza…";
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            _ = SearchAsync(reset: true);
        };

        _categoryBox.ItemsSource = Categories.Select(c => c.Label).ToList();
        _categoryBox.SelectedIndex = 0;
        _categoryBox.Width = 130;
        _categoryBox.Margin = new Thickness(0, 0, 8, 0);
        _categoryBox.ToolTip = "Categoría de las imágenes.";

        _resolutionBox.ItemsSource = Resolutions.Select(r => r.Label).ToList();
        _resolutionBox.SelectedIndex = 0;
        _resolutionBox.Width = 200;
        _resolutionBox.Margin = new Thickness(0, 0, 8, 0);
        _resolutionBox.ToolTip = "Resolución mínima de los resultados.";

        var searchRow = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        searchRow.Children.Add(new TextBlock
        {
            Text = "Buscar",
            Style = (Style)Application.Current.FindResource("Label"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        searchRow.Children.Add(_searchBox);
        searchRow.Children.Add(_categoryBox);
        searchRow.Children.Add(_resolutionBox);

        var searchButton = Kit.Button("Buscar", "PrimaryButton", () => _ = SearchAsync(reset: true));
        searchButton.Padding = new Thickness(18, 6, 18, 6);
        searchRow.Children.Add(searchButton);

        var searchBar = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10)
        };
        searchBar.Child = searchRow;

        // ------------------------- Resultados -------------------------
        _results.Margin = new Thickness(16, 12, 16, 12);
        _scroll = new ScrollViewer
        {
            Content = _results,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        _emptyText.Text = "Escriba un tema y pulse «Buscar» para ver fondos disponibles en internet.";
        _emptyText.Style = (Style)Application.Current.FindResource("Muted");
        _emptyText.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyText.VerticalAlignment = VerticalAlignment.Center;
        _emptyText.TextWrapping = TextWrapping.Wrap;
        _emptyText.TextAlignment = TextAlignment.Center;
        _emptyText.Margin = new Thickness(40);

        var content = new Grid();
        content.Children.Add(_scroll);
        content.Children.Add(_emptyText);

        // ------------------------- Pie -------------------------
        _statusText.Text = "Escriba un término y pulse «Buscar».";
        _statusText.Style = (Style)Application.Current.FindResource("Muted");
        _statusText.FontSize = 11;
        _statusText.VerticalAlignment = VerticalAlignment.Center;

        _moreButton = Kit.Button("Cargar más", "GhostButton", () => _ = SearchAsync(reset: false));
        _moreButton.Padding = new Thickness(14, 5, 14, 5);
        _moreButton.FontSize = 12;
        _moreButton.HorizontalAlignment = HorizontalAlignment.Right;
        _moreButton.Visibility = Visibility.Collapsed;

        // Al cambiar los criterios se oculta la paginación anterior.
        _searchBox.TextChanged += (_, _) => HideMoreButton();
        _categoryBox.SelectionChanged += (_, _) => HideMoreButton();
        _resolutionBox.SelectionChanged += (_, _) => HideMoreButton();

        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.Children.Add(_statusText);
        Grid.SetColumn(_moreButton, 1);
        footerGrid.Children.Add(_moreButton);

        var footer = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 8, 16, 8)
        };
        footer.Child = footerGrid;

        // ------------------------- Encabezado -------------------------
        var titles = new StackPanel();
        titles.Children.Add(new TextBlock
        {
            Text = "Fondos de pantalla en internet",
            FontSize = 18,
            FontWeight = FontWeights.Bold
        });
        titles.Children.Add(new TextBlock
        {
            Text = "Resultados de Wallhaven · solo imágenes SFW en 16:9. Se descargan y se agregan a la " +
                   "galería interna del servidor. Verifique la licencia antes de usarlas.",
            Style = (Style)Application.Current.FindResource("Muted"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        });

        var close = new Button
        {
            Content = "Cerrar",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Padding = new Thickness(14, 7, 14, 7),
            VerticalAlignment = VerticalAlignment.Center
        };
        close.Click += (_, _) => Close();

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(titles);
        Grid.SetColumn(close, 1);
        headerGrid.Children.Add(close);

        var header = new Border
        {
            Background = (Brush)Application.Current.FindResource("HeaderBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };
        header.Child = headerGrid;

        // ------------------------- Composición -------------------------
        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        DockPanel.SetDock(searchBar, Dock.Top);
        root.Children.Add(searchBar);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(content);

        Content = root;

        Loaded += (_, _) =>
        {
            _searchBox.Focus();
            _searchBox.SelectAll();
            _ = SearchAsync(reset: true);
        };
    }

    /// <summary>Cantidad de imágenes agregadas a la galería durante esta sesión.</summary>
    public int AddedCount => _added;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindow.ApplyDarkTitleBar(this);
        NativeWindow.FitToWorkArea(this, 24);
    }

    private async Task SearchAsync(bool reset)
    {
        if (_searching) return;

        var term = _searchBox.Text.Trim();
        if (term.Length == 0)
        {
            SetStatus("Escriba un tema para buscar fondos.");
            return;
        }

        var page = reset ? 1 : _page + 1;
        _searching = true;
        _moreButton.IsEnabled = false;

        if (reset)
        {
            _results.Children.Clear();
            _emptyText.Visibility = Visibility.Collapsed;
            _scroll.Visibility = Visibility.Visible;
            _moreButton.Visibility = Visibility.Collapsed;
            SetStatus("Buscando fondos en internet...");
        }
        else
        {
            SetStatus($"Cargando página {page}...");
        }

        try
        {
            var category = Categories[Math.Max(0, _categoryBox.SelectedIndex)].Value;
            var resolution = Resolutions[Math.Max(0, _resolutionBox.SelectedIndex)];
            var url = $"{ApiBase}?q={Uri.EscapeDataString(term)}&categories={category}&purity=100" +
                      $"&sorting=relevance&order=desc&atleast={resolution.Width}x{resolution.Height}" +
                      $"&ratios=16x9&page={page}";

            using var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"El servicio respondió {(int)response.StatusCode}.");

            await using var stream = await response.Content.ReadAsStreamAsync();
            var payload = await JsonSerializer.DeserializeAsync<SearchResponse>(stream, JsonOptions)
                          ?? new SearchResponse();

            _page = page;
            _term = term;
            _lastPage = Math.Max(1, payload.Meta?.LastPage ?? 1);

            if (payload.Data.Count == 0 && reset)
            {
                ShowMessage($"No se encontraron fondos para «{term}». Pruebe con otro tema o cambie los filtros.");
            }
            else
            {
                foreach (var item in payload.Data)
                    _results.Children.Add(BuildResult(item));
            }

            var total = payload.Meta?.Total ?? _results.Children.Count;
            SetStatus($"{total:N0} resultado(s) · página {_page} de {_lastPage}.", "#22D3EE");
            _moreButton.Visibility = _page < _lastPage ? Visibility.Visible : Visibility.Collapsed;

            if (!reset) _scroll.ScrollToEnd();
        }
        catch (Exception ex)
        {
            SetStatus("No se pudo buscar: " + ex.Message, "#EF4444");
            if (reset && _results.Children.Count == 0)
                ShowMessage("No se pudo conectar con el servicio de fondos. " +
                            "Verifique la conexión a internet e intente de nuevo.");
        }
        finally
        {
            _searching = false;
            _moreButton.IsEnabled = true;
        }
    }

    private Border BuildResult(WallpaperItem item)
    {
        var image = new Image { Height = 130, Stretch = Stretch.UniformToFill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var imageFrame = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(10, 18, 34)),
            ClipToBounds = true,
            Child = image
        };
        _ = LoadThumbnailAsync(item.Thumbs?.Large, image);

        var name = DisplayName(item.Id);

        var texts = new StackPanel { Margin = new Thickness(2, 6, 2, 0) };
        texts.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = item.Resolution
        });
        texts.Children.Add(new TextBlock
        {
            Text = $"{item.Resolution} · {SizeText(item.FileSize)}",
            Style = (Style)Application.Current.FindResource("Muted"),
            FontSize = 11
        });

        var tooBig = item.FileSize > ImageInspector.MaxBytes;
        var add = new Button
        {
            Content = tooBig ? "Muy grande" : "Agregar",
            Style = (Style)Application.Current.FindResource(tooBig ? "GhostButton" : "PrimaryButton"),
            Padding = new Thickness(12, 5, 12, 5),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (tooBig)
        {
            add.IsEnabled = false;
            add.ToolTip = "Supera el máximo de 20 MB permitido por el servidor.";
        }
        else
        {
            add.ToolTip = "Descarga la imagen y la agrega a la galería interna.";
            add.Click += (_, _) => _ = AddAsync(item, add, name);
        }
        texts.Children.Add(add);

        return new Border
        {
            Style = (Style)Application.Current.FindResource("Card"),
            Width = 250,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 12, 12),
            Child = new StackPanel { Children = { imageFrame, texts } }
        };
    }

    private async Task AddAsync(WallpaperItem item, Button button, string name)
    {
        button.IsEnabled = false;
        button.Content = "Agregando...";

        try
        {
            if (!IsWallhavenUrl(item.Path, out var uri))
                throw new Exception("La dirección de la imagen no es válida.");

            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"El servicio respondió {(int)response.StatusCode}.");

            if (response.Content.Headers.ContentLength is long length &&
                length > ImageInspector.MaxBytes)
                throw new Exception($"La imagen pesa {length / 1024d / 1024d:N1} MB y el máximo es 20 MB.");

            var data = await response.Content.ReadAsByteArrayAsync();
            if (data.LongLength > ImageInspector.MaxBytes)
                throw new Exception($"La imagen pesa {data.LongLength / 1024d / 1024d:N1} MB y el máximo es 20 MB.");

            var probe = ImageInspector.Detect(data);
            if (!probe.Valid)
                throw new Exception("El archivo descargado no es una imagen válida.");

            await App.Api.UploadWallpaperAsync($"wallhaven-{item.Id}{probe.Extension}", data,
                probe.ContentType, name);

            _added++;
            button.Content = "Agregado ✓";
            button.Style = (Style)Application.Current.FindResource("SuccessButton");
            button.ToolTip = "Ya está en la galería interna.";
            SetStatus($"«{name}» se agregó a la galería interna.", "#22C55E");
        }
        catch (Exception ex)
        {
            button.Content = "Reintentar";
            button.IsEnabled = true;
            button.Style = (Style)Application.Current.FindResource("PrimaryButton");
            SetStatus("No se pudo agregar: " + ex.Message, "#EF4444");
        }
    }

    private static async Task LoadThumbnailAsync(string? url, Image target)
    {
        if (!IsWallhavenUrl(url, out var uri)) return;

        try
        {
            var data = await Http.GetByteArrayAsync(uri);
            using var stream = new MemoryStream(data);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 500;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            target.Source = bitmap;
        }
        catch
        {
            // La tarjeta queda visible aunque la miniatura no se pueda cargar.
        }
    }

    private void ShowMessage(string text)
    {
        _emptyText.Text = text;
        _emptyText.Visibility = Visibility.Visible;
        _scroll.Visibility = Visibility.Collapsed;
    }

    private void HideMoreButton() => _moreButton.Visibility = Visibility.Collapsed;

    private void SetStatus(string text, string? colorHex = null)
    {
        _statusText.Text = text;
        _statusText.Foreground = colorHex is null
            ? (Brush)Application.Current.FindResource("MutedBrush")
            : BrushFrom(colorHex);
    }

    private string DisplayName(string id)
    {
        var term = string.IsNullOrWhiteSpace(_term) ? "Fondo" : _term.Trim();
        term = char.ToUpperInvariant(term[0]) + term[1..];
        return $"{term} · {id}";
    }

    private static string SizeText(long bytes)
        => bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:N1} MB"
            : $"{Math.Max(1, bytes / 1024d):N0} KB";

    private static Brush BrushFrom(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return Brushes.Gray; }
    }

    /// <summary>Solo se aceptan direcciones HTTPS servidas por Wallhaven.</summary>
    private static bool IsWallhavenUrl(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps) return false;

        var host = parsed.Host;
        if (!host.Equals("wallhaven.cc", StringComparison.OrdinalIgnoreCase) &&
            !host.EndsWith(".wallhaven.cc", StringComparison.OrdinalIgnoreCase))
            return false;

        uri = parsed;
        return true;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CyberManager/1.0 (WPF admin console)");
        return client;
    }

    // ------------------------- Respuesta de Wallhaven -------------------------

    private sealed class SearchResponse
    {
        public List<WallpaperItem> Data { get; set; } = new();
        public SearchMeta? Meta { get; set; }
    }

    private sealed class SearchMeta
    {
        [JsonPropertyName("current_page")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("last_page")]
        public int LastPage { get; set; }

        public long Total { get; set; }
    }

    private sealed class WallpaperItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;

        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }

        public WallpaperThumbs? Thumbs { get; set; }
    }

    private sealed class WallpaperThumbs
    {
        public string Large { get; set; } = string.Empty;
    }
}
