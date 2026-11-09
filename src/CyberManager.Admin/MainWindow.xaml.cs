using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Admin.Services;
using CyberManager.Admin.Views;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin;

public partial class MainWindow : Window
{
    private const int MaxLogEntries = 250;

    private readonly Dictionary<string, UserControl> _views = new();
    private readonly DispatcherTimer _headerTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly Dictionary<int, string> _stationNames = new();
    private readonly Dictionary<int, StationStatus> _stationStatus = new();

    private string _current = "dashboard";
    private bool? _connected;
    private AppLanguage _language = L.Language;
    private UpdateInfoDto? _update;

    public MainWindow()
    {
        InitializeComponent();

        UserText.Text = App.Session.FullName;
        VersionText.Text = $"CyberManager {AppInfo.Version}";
        UpdateWindowTitle();
        UpdateRoleText();

        App.NavigationRequested += OnNavigationRequested;
        AppearanceService.Changed += ApplyAppearance;
        ApplyAppearance(AppearanceService.Current);
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        _headerTimer.Tick += async (_, _) => await RefreshHeaderAsync();
    }

    private void UpdateRoleText()
        => RoleText.Text = Labels.RoleName(App.Session.Role);

    /// <summary>Muestra el nombre y la versión del software en la barra de título de Windows.</summary>
    private void UpdateWindowTitle()
        => Title = $"{L.T("CyberManager · Administración")} · v{AppInfo.Version}";

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsShuttingDown && !App.IsSessionEnding)
        {
            var message = L.T("¿Cerrar la consola de administración?") + Environment.NewLine +
                          Environment.NewLine +
                          L.T("El servidor seguirá ejecutándose en segundo plano.");
            var result = MessageBox.Show(this, message, L.T("Cerrar CyberManager"),
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Barra de título nativa de Windows: los iconos de minimizar/cerrar siempre son visibles.
        NativeWindow.ApplyDarkTitleBar(this);
        NativeWindow.FitToWorkArea(this);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(() => NativeWindow.FitToWorkArea(this));
    }

    /// <summary>Ajusta el tamaño de los iconos y textos del menú lateral.</summary>
    private void ApplyAppearance(AppearanceSettings settings)
    {
        var languageChanged = _language != L.Language;
        _language = L.Language;

        foreach (var button in NavPanel.Children.OfType<Button>())
        {
            button.FontSize = settings.NavFontSize;
            if (button.Content is not StackPanel panel) continue;

            if (panel.Children.Count > 0 && panel.Children[0] is TextBlock icon)
            {
                icon.FontSize = settings.NavIconSize;
                icon.Margin = new Thickness(0, 0, 10 + Math.Max(0, settings.NavIconSize - 16) / 2, 0);
            }

            if (panel.Children.Count > 1 && panel.Children[1] is TextBlock label)
                label.FontSize = settings.NavFontSize;
        }

        if (!languageChanged) return;

        // Al cambiar el idioma se traduce la ventana y se reconstruyen las vistas
        // para que los textos generados por código también queden traducidos.
        Loc.Apply(this);
        UpdateWindowTitle();
        UpdateRoleText();
        ConnectionText.Text = L.T(ConnectionText.Text);
        if (_update is not null) ApplyUpdate(_update);
        _views.Clear();
        Navigate(_current);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NativeWindow.FitToWorkArea(this);
        Loc.Apply(this);
        UpdateRoleText();
        AddLog(L.T("Consola"), "Sesión iniciada en la consola de administración.", "#22D3EE");

        await App.InitializeRealtimeAsync();
        await RefreshHeaderAsync();
        Navigate("dashboard");
        _headerTimer.Start();

        // La consulta de actualizaciones se hace una sola vez, al abrir la consola.
        _ = RefreshUpdateBannerAsync(force: true);

        if (App.Realtime is not null)
        {
            App.Realtime.ConnectionChanged += OnConnectionChanged;
            App.Realtime.EventReceived += OnRealtimeEvent;
            App.Realtime.StationStateChanged += OnRealtimeStationState;
            App.Realtime.UpdateAvailable += OnUpdateAvailable;
            OnConnectionChanged(App.Realtime.IsConnected);
        }
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        App.NavigationRequested -= OnNavigationRequested;
        AppearanceService.Changed -= ApplyAppearance;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _headerTimer.Stop();

        if (App.Realtime is not null)
        {
            App.Realtime.ConnectionChanged -= OnConnectionChanged;
            App.Realtime.EventReceived -= OnRealtimeEvent;
            App.Realtime.StationStateChanged -= OnRealtimeStationState;
            App.Realtime.UpdateAvailable -= OnUpdateAvailable;
        }

        await App.DisposeRealtimeAsync();
        App.ShutdownApplication();
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea) && WindowState == WindowState.Normal)
            Dispatcher.BeginInvoke(() => NativeWindow.FitToWorkArea(this));
    }

    private void OnNavigationRequested(string key) => Dispatcher.Invoke(() => Navigate(key));

    private void OnConnectionChanged(bool connected) => Dispatcher.Invoke(() =>
    {
        ConnectionDot.Fill = (Brush)FindResource(connected ? "SuccessBrush" : "WarningBrush");
        ConnectionText.Text = connected ? L.T("En línea · tiempo real") : L.T("Reconectando...");

        if (_connected == connected) return;
        _connected = connected;

        AddLog("Conexión",
            connected ? "Servidor en línea · tiempo real activo." : "Conexión perdida. Reconectando...",
            connected ? "#22C55E" : "#F59E0B");
    });

    private void Navigate(string key)
    {
        if (!_views.TryGetValue(key, out var view))
        {
            view = key switch
            {
                "pos" => new PosView(),
                "cash" => new CashView(),
                "products" => new ProductsView(),
                "inventory" => new InventoryView(),
                "purchases" => new PurchasesView(),
                "stations" => new StationsView(),
                "customers" => new CustomersView(),
                "reservations" => new ReservationsView(),
                "wallpapers" => new WallpapersView(),
                "promos" => new PromosView(),
                "reports" => new ReportsView(),
                "settings" => new SettingsView(),
                _ => new DashboardView()
            };
            _views[key] = view;
        }

        _current = key;
        ContentHost.Content = view;

        foreach (var button in NavPanel.Children.OfType<Button>())
            button.Tag = string.Equals(button.CommandParameter as string, key, StringComparison.Ordinal) ? "active" : null;

        if (view is IRefreshable refreshable)
            _ = Ui.Run(refreshable.RefreshAsync, "No se pudieron cargar los datos");
    }

    public void RefreshCurrent()
    {
        if (_views.TryGetValue(_current, out var view) && view is IRefreshable refreshable)
            _ = Ui.Run(refreshable.RefreshAsync, "No se pudieron actualizar los datos");
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string key }) Navigate(key);
    }

    private async Task RefreshHeaderAsync()
    {
        try
        {
            var rate = await App.Api.TodayRateAsync();
            RateText.Text = rate.Rate > 0 ? $"1 USD = {rate.Rate:N2} Bs" : L.T("Tasa sin definir");

            var settings = await App.Api.SettingsAsync();
            if (settings.TryGetValue("BusinessName", out var business) && !string.IsNullOrWhiteSpace(business))
                BusinessText.Text = business;

            var stations = await App.Api.StationsAsync();
            foreach (var station in stations)
            {
                _stationNames[station.Id] = station.Name;
                _stationStatus[station.Id] = station.Status;
            }

            OnConnectionChanged(App.Realtime?.IsConnected == true);
        }
        catch
        {
            ConnectionDot.Fill = (Brush)FindResource("DangerBrush");
            ConnectionText.Text = L.T("Sin conexión");
        }
    }

    // ===================== Aviso de nueva versión =====================

    private async Task RefreshUpdateBannerAsync(bool force = false)
    {
        try
        {
            ApplyUpdate(await App.Api.LatestUpdateAsync(force));
        }
        catch
        {
            // Un fallo del aviso no debe marcar la consola como desconectada.
        }
    }

    private void ApplyUpdate(UpdateInfoDto info)
    {
        _update = info;

        if (!info.IsReminderDue || string.IsNullOrWhiteSpace(info.LatestVersion))
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateTitle.Text = $"{L.T("Nueva versión disponible")}: {info.LatestVersion}";

        var published = info.PublishedUtc?.ToLocalTime();
        UpdateText.Text = published is DateTime date
            ? $"{L.T("Versión publicada:")} {date:dd/MM/yyyy HH:mm} · " +
              $"{L.T("Versión instalada:")} {info.CurrentVersion}"
            : $"{L.T("Versión instalada:")} {info.CurrentVersion}";

        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void OnUpdateAvailable(UpdateInfoDto update) => Dispatcher.BeginInvoke(() =>
    {
        ApplyUpdate(update);
        AddLog(L.T("Actualización disponible"),
            $"{L.T("Versión")} {update.LatestVersion} · {L.T("Versión instalada:")} {update.CurrentVersion}",
            "#22D3EE");
    });

    private void OpenUpdate_Click(object sender, RoutedEventArgs e)
    {
        var url = _update?.ReleaseUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message, L.T("Actualización"));
        }
    }

    private void DismissUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_update?.LatestVersion is not string version) return;

        _ = Ui.Run(async () => ApplyUpdate(await App.Api.DismissUpdateAsync(version)),
            L.T("No se pudo ocultar el aviso"));
    }

    // ===================== Bitácora de terminales =====================

    private void OnRealtimeEvent(RealtimeEvent e) => Dispatcher.BeginInvoke(() =>
    {
        var (title, color) = e.Type switch
        {
            "SessionStarted" => ("Sesión iniciada", "#22C55E"),
            "SessionEnded" => ("Sesión cerrada", "#3B82F6"),
            "SessionWarning" => ("Aviso de sesión", "#F59E0B"),
            "Overtime" => ("Tiempo agotado", "#EF4444"),
            "StationOnline" => ("Equipo en línea", "#22C55E"),
            "StationOffline" => ("Equipo sin conexión", "#F59E0B"),
            "AgentOnline" => ("Agente conectado", "#22C55E"),
            "AgentOffline" => ("Agente desconectado", "#F59E0B"),
            "StationMaintenance" => ("Mantenimiento", "#8B5CF6"),
            "CommandSent" => ("Comando enviado", "#22D3EE"),
            "CommandResult" => ("Resultado de comando", "#8FA4C4"),
            "WallpaperChanged" => ("Fondos actualizados", "#8B5CF6"),
            _ => (e.Type, "#8FA4C4")
        };

        AddLog(title, string.IsNullOrWhiteSpace(e.Message) ? e.Type : e.Message!, color);
    });

    private void OnRealtimeStationState(StationStateEvent e) => Dispatcher.BeginInvoke(() =>
    {
        if (_stationStatus.TryGetValue(e.StationId, out var previous) && previous == e.Status) return;
        _stationStatus[e.StationId] = e.Status;

        var name = _stationNames.TryGetValue(e.StationId, out var stationName)
            ? stationName
            : $"PC #{e.StationId}";

        var text = Labels.StatusName(e.Status);
        if (e.Status == StationStatus.InUse && e.SessionId is int sessionId)
            text += $" · sesión #{sessionId}";

        AddLog(name, text, Labels.StatusColor(e.Status));
    });

    private void AddLog(string title, string message, string colorHex)
    {
        var accent = ParseColor(colorHex);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new Border
        {
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 9, 0)
        });

        var texts = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(accent),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var time = new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm:ss"),
            FontSize = 10,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(time, 1);
        header.Children.Add(time);

        texts.Children.Add(header);
        texts.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        });

        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        LogPanel.Children.Add(new Border
        {
            Background = (Brush)FindResource("SurfaceAltBrush"),
            BorderBrush = (Brush)FindResource("GridLineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 7),
            Child = grid
        });

        while (LogPanel.Children.Count > MaxLogEntries)
            LogPanel.Children.RemoveAt(0);

        var count = LogPanel.Children.Count;
        LogCountText.Text = $"{count} evento{(count == 1 ? "" : "s")}";
        LogScroll.ScrollToEnd();
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0x8F, 0xA4, 0xC4); }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogPanel.Children.Clear();
        LogCountText.Text = "0 eventos";
    }
}
