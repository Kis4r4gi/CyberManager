using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberManager.Admin.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>
/// Visualizador remoto: pide capturas de pantalla a una estación y las muestra en vivo.
/// La estación captura desde su propia sesión de usuario y las envía por SignalR.
/// </summary>
public sealed class ScreenViewerWindow : Window
{
    private readonly int _stationId;
    private readonly string _stationName;
    private readonly Image _image = new() { Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
    private readonly TextBlock _statusText;
    private readonly TextBlock _stationText;
    private readonly CheckBox _autoRefresh;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    private DateTime _lastRequest = DateTime.MinValue;

    public ScreenViewerWindow(int stationId, string stationName)
    {
        _stationId = stationId;
        _stationName = stationName;

        Title = $"Pantalla remota · {stationName}";
        Width = 1160;
        Height = 800;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("BgBrush");

        var root = new DockPanel { LastChildFill = true };

        // ------------------------- Encabezado -------------------------
        var header = new Border
        {
            Background = (Brush)Application.Current.FindResource("HeaderBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titles = new StackPanel();
        titles.Children.Add(new TextBlock { Text = stationName, FontSize = 18, FontWeight = FontWeights.Bold });
        _stationText = new TextBlock
        {
            Text = "Solicitando primera captura...",
            Style = (Style)Application.Current.FindResource("Muted"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        };
        titles.Children.Add(_stationText);
        headerGrid.Children.Add(titles);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        _autoRefresh = new CheckBox
        {
            Content = "Actualizar automáticamente",
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };
        actions.Children.Add(_autoRefresh);

        var capture = new Button
        {
            Content = "Capturar ahora",
            Style = (Style)Application.Current.FindResource("BlueButton"),
            Padding = new Thickness(14, 7, 14, 7)
        };
        capture.Click += async (_, _) => await RequestAsync(force: true);
        actions.Children.Add(capture);

        Grid.SetColumn(actions, 1);
        headerGrid.Children.Add(actions);
        header.Child = headerGrid;

        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ------------------------- Pie de estado -------------------------
        var footer = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 8, 16, 8)
        };
        _statusText = new TextBlock
        {
            Text = "Esperando captura del equipo...",
            Style = (Style)Application.Current.FindResource("Muted"),
            FontSize = 11
        };
        footer.Child = _statusText;

        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ------------------------- Imagen -------------------------
        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(3, 6, 12)),
            Margin = new Thickness(12),
            CornerRadius = new CornerRadius(10),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1.5),
            Child = _image
        });

        Content = root;

        Loaded += OnLoaded;
        Closed += OnClosed;
        _timer.Tick += async (_, _) =>
        {
            if (_autoRefresh.IsChecked == true) await RequestAsync();

            if (_image.Source is null && _lastRequest != DateTime.MinValue &&
                DateTime.UtcNow - _lastRequest > TimeSpan.FromSeconds(5))
            {
                SetStatus("El equipo no responde. Verifique que la estación esté encendida y en línea.",
                    "#F59E0B");
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindow.ApplyDarkTitleBar(this);
        NativeWindow.FitToWorkArea(this, 24, centerOnScreen: false);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (App.Realtime is not null)
            App.Realtime.ScreenFrameReceived += OnScreenFrame;

        _timer.Start();
        await RequestAsync(force: true);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (App.Realtime is not null)
            App.Realtime.ScreenFrameReceived -= OnScreenFrame;
    }

    private async Task RequestAsync(bool force = false)
    {
        if (App.Realtime?.IsConnected != true)
        {
            SetStatus("Sin conexión con el servidor. Se reintentará automáticamente.", "#F59E0B");
            return;
        }

        var now = DateTime.UtcNow;
        if (!force && now - _lastRequest < TimeSpan.FromSeconds(1.5)) return;
        _lastRequest = now;

        try
        {
            await App.Realtime.RequestScreenAsync(_stationId, 1400, 65);
            if (_image.Source is null)
                SetStatus("Solicitud enviada. Esperando respuesta del equipo...", "#22D3EE");
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo solicitar la captura: {ex.Message}", "#EF4444");
        }
    }

    private void OnScreenFrame(ScreenFrameResult frame)
    {
        if (frame.StationId != _stationId) return;
        Dispatcher.BeginInvoke(() => ApplyFrame(frame));
    }

    private void ApplyFrame(ScreenFrameResult frame)
    {
        if (!string.IsNullOrWhiteSpace(frame.Error) || string.IsNullOrWhiteSpace(frame.ImageBase64))
        {
            SetStatus($"No se pudo capturar: {frame.Error ?? "respuesta vacía"}", "#EF4444");
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(frame.ImageBase64);
            using var stream = new MemoryStream(bytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            _image.Source = bitmap;
            _stationText.Text = $"{_stationName} · captura en vivo";

            var latency = Math.Max(0, (DateTime.UtcNow - frame.CapturedAtUtc).TotalSeconds);
            SetStatus($"Actualizado {DateTime.Now:HH:mm:ss} · {frame.Width}×{frame.Height} px · {latency:0.0} s de retardo",
                "#22C55E");
        }
        catch (Exception ex)
        {
            SetStatus($"Captura inválida: {ex.Message}", "#EF4444");
        }
    }

    private void SetStatus(string text, string colorHex)
    {
        _statusText.Text = text;
        _statusText.Foreground = BrushFrom(colorHex);
    }

    private static Brush BrushFrom(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return Brushes.Gray; }
    }
}
