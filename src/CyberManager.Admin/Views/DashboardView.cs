using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Admin.Services;
using CyberManager.Desktop.Api;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

public sealed class DashboardView : UserControl, IRefreshable
{
    private readonly WrapPanel _stationsPanel = new();
    private readonly TextBlock _updatedText = Kit.Muted("Cargando...");
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _autoRefresh = new() { Interval = TimeSpan.FromSeconds(30) };

    private readonly TextBlock _onlineValue;
    private readonly TextBlock _sessionsValue;
    private readonly TextBlock _salesValue;
    private readonly TextBlock _salesBsValue;
    private readonly TextBlock _cashValue;
    private readonly TextBlock _alertsValue;

    // ----- Personalización del tablero -----
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _sortBox = new();
    private readonly ComboBox _sizeBox = new();
    private readonly ComboBox _zoneBox = new();
    private readonly CheckBox _onlyInUseBox = new();
    private readonly CheckBox _metricsBox = new();
    private readonly CheckBox _legendBox = new();
    private readonly Button _orderButton;
    private readonly FrameworkElement _metricsPanel;
    private readonly TextBlock _legend;
    private Border? _toolbarPanel;
    private Button? _toolbarToggle;
    private bool _toolbarLoading;

    private DashboardDto? _dashboard;
    private List<StationDto> _stations = new();
    private List<SessionDto> _sessions = new();
    private List<RateDto> _rates = new();
    private List<CustomerDto> _customers = new();
    private List<TimePackageDto> _packages = new();
    private List<ZoneDto> _zones = new();

    private static readonly string[] SortKeys = { "manual", "name", "status", "zone", "kind", "time", "amount" };
    private static readonly string[] SortLabels =
    {
        "Manual", "Nombre", "Estado", "Zona", "Tipo de equipo", "Tiempo", "Importe"
    };
    private static readonly int[] SizePresets = { 80, 90, 100, 110, 120, 130, 140, 150, 160 };

    public DashboardView()
    {
        _orderButton = Kit.Button("↑ Ascendente", "GhostButton", ToggleOrder);
        _legend = Kit.Muted("Verde: disponible · Celeste: en uso · Ámbar: en pausa · Violeta: mantenimiento");
        _legend.HorizontalAlignment = HorizontalAlignment.Right;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _toolbarToggle = Kit.Button("Configuración", "GhostButton", ToggleToolbar);
        if (App.Session.Role == UserRole.Cashier)
            _toolbarToggle.Visibility = Visibility.Collapsed;
        root.Children.Add(Kit.PageHeader("Tablero de equipos",
            "Estado en vivo de cada PC, sesiones y movimientos del día.",
            _toolbarToggle,
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync()),
            Kit.Button("Nuevo equipo", "PrimaryButton", () =>
            {
                var dialog = new StationDialog(_zones, _rates) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() != true || dialog.Result is null) return;
                _ = Ui.Run(async () =>
                {
                    await App.Api.SaveStationAsync(null, dialog.Result);
                    await RefreshAsync();
                }, "No se pudo crear el equipo");
            }),
            Kit.Button("Ver equipos", "GhostButton", () => App.RequestNavigation("stations"))));

        // La barra de personalización queda oculta tras el botón «Configuración».
        _toolbarPanel = BuildToolbar();
        _toolbarPanel.Visibility = Visibility.Collapsed;
        Grid.SetRow(_toolbarPanel, 1);
        root.Children.Add(_toolbarPanel);

        // Los terminales van primero: ocupan el área principal del tablero.
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _stationsPanel
        };
        _stationsPanel.Margin = new Thickness(0, 0, 0, 8);

        var footer = new Grid { Margin = new Thickness(0, 4, 8, 6) };
        footer.Children.Add(_updatedText);
        footer.Children.Add(_legend);

        var container = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        container.Children.Add(footer);
        container.Children.Add(scroll);
        Grid.SetRow(container, 2);
        root.Children.Add(container);

        // Resumen compacto al pie, después de los terminales.
        var metrics = new UniformGrid { Columns = 6, Margin = new Thickness(0, 4, 0, 0) };
        metrics.Children.Add(Metric("Equipos en línea", "AccentBrush", out _onlineValue));
        metrics.Children.Add(Metric("Sesiones activas", "Accent2Brush", out _sessionsValue));
        metrics.Children.Add(Metric("Ventas del día", "SuccessBrush", out _salesValue));
        metrics.Children.Add(Metric("En bolívares", "PurpleBrush", out _salesBsValue));
        metrics.Children.Add(Metric("Caja del turno", "WarningBrush", out _cashValue));
        metrics.Children.Add(Metric("Alertas", "DangerBrush", out _alertsValue));
        _metricsPanel = metrics;
        Grid.SetRow(metrics, 3);
        root.Children.Add(metrics);

        Content = root;

        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RefreshAsync();
        };
        _autoRefresh.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            Subscribe();
            AppearanceService.Changed += OnAppearanceChanged;
            _autoRefresh.Start();
            SyncToolbar();
            await RefreshAsync();
            _ = CheckForUpdatesAsync();
        };
        Unloaded += (_, _) =>
        {
            Unsubscribe();
            AppearanceService.Changed -= OnAppearanceChanged;
            _autoRefresh.Stop();
        };
    }

    // ---------------------------------------------------------------------
    // Barra de personalización
    // ---------------------------------------------------------------------

    private Border BuildToolbar()
    {
        _searchBox.Width = 200;
        _searchBox.Margin = new Thickness(0, 0, 8, 0);
        _searchBox.VerticalAlignment = VerticalAlignment.Center;
        _searchBox.ToolTip = L.T("Buscar por nombre, zona, tipo o usuario");
        _searchBox.TextChanged += (_, _) => OnToolbarChanged(render: true);

        _sortBox.Width = 120;
        _sortBox.Margin = new Thickness(0, 0, 8, 0);
        _sortBox.VerticalAlignment = VerticalAlignment.Center;
        _sortBox.ItemsSource = SortKeys
            .Select((key, i) => new Option<string>(key, SortLabels[i]))
            .ToList();
        _sortBox.DisplayMemberPath = nameof(Option<string>.DisplayText);
        _sortBox.SelectionChanged += (_, _) => OnToolbarChanged(render: true);

        _orderButton.Margin = new Thickness(0, 0, 8, 0);
        _orderButton.Padding = new Thickness(10, 5, 10, 5);
        _orderButton.FontSize = 12;

        _sizeBox.Width = 72;
        _sizeBox.Margin = new Thickness(0, 0, 8, 0);
        _sizeBox.VerticalAlignment = VerticalAlignment.Center;
        _sizeBox.ItemsSource = SizePresets
            .Select(size => new Option<int>(size, $"{size} %"))
            .ToList();
        _sizeBox.DisplayMemberPath = nameof(Option<int>.DisplayText);
        _sizeBox.SelectionChanged += (_, _) =>
        {
            if (_toolbarLoading || _sizeBox.SelectedItem is not Option<int> option) return;
            AppearanceService.Current.StationCardScale = option.Value;
            AppearanceService.Save();
            Render();
        };

        _zoneBox.Width = 130;
        _zoneBox.Margin = new Thickness(0, 0, 8, 0);
        _zoneBox.VerticalAlignment = VerticalAlignment.Center;
        _zoneBox.DisplayMemberPath = nameof(Option<int?>.DisplayText);
        _zoneBox.SelectionChanged += (_, _) => OnToolbarChanged(render: true);

        _onlyInUseBox.Content = L.T("Solo en uso");
        _onlyInUseBox.Margin = new Thickness(0, 0, 10, 0);
        _onlyInUseBox.VerticalAlignment = VerticalAlignment.Center;
        _onlyInUseBox.Checked += (_, _) => OnToolbarChanged(render: true);
        _onlyInUseBox.Unchecked += (_, _) => OnToolbarChanged(render: true);

        _metricsBox.Content = L.T("Métricas");
        _metricsBox.Margin = new Thickness(0, 0, 10, 0);
        _metricsBox.VerticalAlignment = VerticalAlignment.Center;
        _metricsBox.Checked += (_, _) => OnToolbarChanged(render: false);
        _metricsBox.Unchecked += (_, _) => OnToolbarChanged(render: false);

        _legendBox.Content = L.T("Leyenda");
        _legendBox.Margin = new Thickness(0, 0, 0, 0);
        _legendBox.VerticalAlignment = VerticalAlignment.Center;
        _legendBox.Checked += (_, _) => OnToolbarChanged(render: false);
        _legendBox.Unchecked += (_, _) => OnToolbarChanged(render: false);

        var row = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(_searchBox);
        row.Children.Add(_sortBox);
        row.Children.Add(_orderButton);
        row.Children.Add(_sizeBox);
        row.Children.Add(_zoneBox);
        row.Children.Add(_onlyInUseBox);
        row.Children.Add(_metricsBox);
        row.Children.Add(_legendBox);

        var border = Kit.Panel(row);
        border.Margin = new Thickness(0, 0, 0, 12);
        return border;
    }

    /// <summary>Muestra u oculta la barra de personalización del tablero.</summary>
    private void ToggleToolbar()
    {
        if (_toolbarPanel is null) return;

        var show = _toolbarPanel.Visibility != Visibility.Visible;
        _toolbarPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (_toolbarToggle is not null)
            _toolbarToggle.Content = show ? L.T("Ocultar configuración") : L.T("Configuración");
    }

    private void SyncToolbar()
    {
        _toolbarLoading = true;
        try
        {
            var settings = AppearanceService.Current;

            _searchBox.Text = settings.DashboardSearch ?? string.Empty;

            var sortIndex = Array.IndexOf(SortKeys,
                AppearanceService.NormalizeDashboardSort(settings.DashboardSort));
            _sortBox.SelectedIndex = sortIndex >= 0 ? sortIndex : 0;

            var scale = (int)Math.Round(Math.Clamp(settings.StationCardScale, 80, 160));
            var sizeIndex = Array.IndexOf(SizePresets, SizePresets
                .OrderBy(s => Math.Abs(s - scale)).First());
            _sizeBox.SelectedIndex = sizeIndex >= 0 ? sizeIndex : Array.IndexOf(SizePresets, 100);

            RebuildZoneOptions(settings.DashboardZoneId);

            _onlyInUseBox.IsChecked = settings.DashboardOnlyInUse;
            _metricsBox.IsChecked = settings.DashboardShowMetrics;
            _legendBox.IsChecked = settings.DashboardShowLegend;
            _orderButton.Content = settings.DashboardSortDescending
                ? L.T("↓ Descendente")
                : L.T("↑ Ascendente");
        }
        finally
        {
            _toolbarLoading = false;
        }

        ApplyVisibility();
    }

    /// <summary>Reconstruye el filtro de zonas conservando la selección guardada.</summary>
    private void RebuildZoneOptions(int? selectedZone)
    {
        var options = new List<Option<int?>>
        {
            new(null, "Todas las zonas")
        };
        options.AddRange(_zones
            .Where(z => z.Active)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Name)
            .Select(z => new Option<int?>(z.Id, z.Name)));

        var index = selectedZone is int id
            ? options.FindIndex(o => o.Value == id)
            : 0;
        if (index < 0) index = 0;

        var restore = _toolbarLoading;
        _toolbarLoading = true;
        try
        {
            _zoneBox.ItemsSource = options;
            _zoneBox.SelectedIndex = index;
        }
        finally
        {
            _toolbarLoading = restore;
        }
    }

    private void OnToolbarChanged(bool render)
    {
        if (_toolbarLoading) return;

        var settings = AppearanceService.Current;
        settings.DashboardSearch = _searchBox.Text.Trim();
        settings.DashboardSort = _sortBox.SelectedItem is Option<string> sort
            ? sort.Value
            : "manual";
        settings.DashboardZoneId = _zoneBox.SelectedItem is Option<int?> zone ? zone.Value : null;
        settings.DashboardOnlyInUse = _onlyInUseBox.IsChecked == true;
        settings.DashboardShowMetrics = _metricsBox.IsChecked == true;
        settings.DashboardShowLegend = _legendBox.IsChecked == true;
        AppearanceService.Save();

        ApplyVisibility();
        if (render) Render();
    }

    private void ToggleOrder()
    {
        var settings = AppearanceService.Current;
        settings.DashboardSortDescending = !settings.DashboardSortDescending;
        _orderButton.Content = settings.DashboardSortDescending
            ? L.T("↓ Descendente")
            : L.T("↑ Ascendente");
        AppearanceService.Save();
        Render();
    }

    private void ApplyVisibility()
    {
        var settings = AppearanceService.Current;
        _metricsPanel.Visibility = settings.DashboardShowMetrics ? Visibility.Visible : Visibility.Collapsed;
        _legend.Visibility = settings.DashboardShowLegend ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAppearanceChanged(AppearanceSettings settings)
    {
        if (!IsLoaded) return;
        Dispatcher.BeginInvoke(() =>
        {
            SyncToolbar();
            Render();
        });
    }

    private static Border Metric(string label, string accent, out TextBlock valueText)
    {
        var card = Kit.StatCard(label, "—", null, accent, out var value);
        card.Margin = new Thickness(0, 0, 10, 0);
        valueText = value;
        return card;
    }

    private void Subscribe()
    {
        if (App.Realtime is null) return;
        Unsubscribe();
        App.Realtime.StationStateChanged += OnStationState;
        App.Realtime.EventReceived += OnEvent;
    }

    private void Unsubscribe()
    {
        if (App.Realtime is null) return;
        App.Realtime.StationStateChanged -= OnStationState;
        App.Realtime.EventReceived -= OnEvent;
    }

    private void OnStationState(StationStateEvent e) => Dispatcher.BeginInvoke(() =>
    {
        _debounce.Stop();
        _debounce.Start();
    });

    private void OnEvent(RealtimeEvent e) => Dispatcher.BeginInvoke(() =>
    {
        _debounce.Stop();
        _debounce.Start();
    });

    public async Task RefreshAsync()
    {
        try
        {
            var dashboardTask = App.Api.DashboardAsync();
            var stationsTask = App.Api.StationsAsync();
            var sessionsTask = App.Api.ActiveSessionsAsync();
            var ratesTask = App.Api.RatesAsync();
            var customersTask = App.Api.CustomersAsync();
            var packagesTask = App.Api.PackagesAsync();
            var zonesTask = App.Api.ZonesAsync();

            await Task.WhenAll(dashboardTask, stationsTask, sessionsTask, ratesTask, customersTask,
                packagesTask, zonesTask);

            _dashboard = await dashboardTask;
            _stations = await stationsTask;
            _sessions = await sessionsTask;
            _rates = await ratesTask;
            _customers = await customersTask;
            _packages = await packagesTask;
            _zones = await zonesTask;

            RebuildZoneOptions(AppearanceService.Current.DashboardZoneId);
            Render();
        }
        catch (Exception ex)
        {
            _updatedText.Text = $"Sin conexión con el servidor · {ex.Message}";
        }
    }

    private void Render()
    {
        if (_dashboard is not { } d) return;

        var rate = d.ExchangeRate;

        _onlineValue.Text = $"{d.OnlineStations}/{d.TotalStations}";
        _sessionsValue.Text = d.ActiveSessions.ToString();
        _salesValue.Text = $"$ {d.TodayTotal:N2}";
        _salesBsValue.Text = $"Bs {d.TodayTotalVes:N2}";
        _cashValue.Text = $"$ {d.CashInDrawer:N2}";
        _alertsValue.Text = d.LowStockCount == 0 ? "Todo en orden" : $"{d.LowStockCount} stock bajo";

        _onlineValue.ToolTip = $"{d.AvailableStations} disponibles · {d.InUseStations} en uso · {d.PausedStations} en pausa";
        _sessionsValue.ToolTip = $"{d.TodaySessions} sesiones iniciadas hoy";
        _salesValue.ToolTip = $"Productos $ {d.TodaySales:N2} · Tiempo $ {d.TodayTimeSales:N2}";
        _salesBsValue.ToolTip = $"Tasa aplicada: 1 USD = {rate:N2} Bs";
        _cashValue.ToolTip = $"En bolívares: Bs {d.CashInDrawerVes:N2}";
        _alertsValue.ToolTip = $"{d.PendingReservations} reservas pendientes · {d.LowStockCount} productos bajo el mínimo";

        _updatedText.Text = $"Actualizado {DateTime.Now:HH:mm:ss} · 1 USD = {rate:N2} Bs · " +
                            $"{d.PendingReservations} reservas pendientes";

        _stationsPanel.Children.Clear();
        var visible = FilterStations(d.Stations).ToList();
        if (visible.Count == 0)
        {
            _stationsPanel.Children.Add(EmptyState());
            return;
        }

        foreach (var station in visible)
            _stationsPanel.Children.Add(BuildTile(station, rate));
    }

    /// <summary>Aplica búsqueda, zona, filtro de uso y orden configurados.</summary>
    private IEnumerable<StationSummaryDto> FilterStations(IEnumerable<StationSummaryDto> source)
    {
        var settings = AppearanceService.Current;
        var search = (settings.DashboardSearch ?? string.Empty).Trim();

        var query = source.AsEnumerable();

        if (settings.DashboardZoneId is int zoneId)
            query = query.Where(s => FullStation(s.Id)?.ZoneId == zoneId);

        if (settings.DashboardOnlyInUse)
            query = query.Where(s => s.SessionId is not null ||
                                     s.Status is StationStatus.InUse or StationStatus.Paused);

        if (search.Length > 0)
            query = query.Where(s => Matches(s, search));

        return SortStations(query, settings.DashboardSort, settings.DashboardSortDescending);
    }

    private StationDto? FullStation(int id) => _stations.FirstOrDefault(s => s.Id == id);

    private bool Matches(StationSummaryDto station, string search)
    {
        if (station.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)) return true;
        if ((station.ZoneName ?? string.Empty).Contains(search, StringComparison.CurrentCultureIgnoreCase))
            return true;

        var full = FullStation(station.Id);
        if (full is null) return false;

        return Labels.StationKindName(full.Kind)
                   .Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               (full.Platform ?? string.Empty)
                   .Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               (full.CurrentUser ?? string.Empty)
                   .Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private IEnumerable<StationSummaryDto> SortStations(IEnumerable<StationSummaryDto> source,
        string sort, bool descending)
    {
        IOrderedEnumerable<StationSummaryDto> ordered = sort switch
        {
            "name" => source.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),

            "status" => source.OrderBy(s => s.Status)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),

            "zone" => source.OrderBy(s => s.ZoneName ?? string.Empty,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),

            "kind" => source.OrderBy(s => (int)(FullStation(s.Id)?.Kind ?? StationKind.Pc))
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),

            // Las sesiones sin tiempo (equipos libres) van al final del orden ascendente.
            "time" => source.OrderBy(s => s.SessionId is null ? int.MaxValue
                    : s.SessionType == SessionType.Prepaid ? s.RemainingMinutes : s.ElapsedMinutes)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),

            "amount" => source.OrderBy(s => s.SessionId is null ? -1m : s.CurrentAmount)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase),

            _ => source.OrderBy(s => FullStation(s.Id)?.SortOrder ?? int.MaxValue)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        return descending ? ordered.Reverse() : ordered;
    }

    private static FrameworkElement EmptyState()
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(24, 36, 24, 36)
        };
        panel.Children.Add(new TextBlock
        {
            Text = L.T("No hay equipos que coincidan con los filtros."),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = L.T("Ajuste la búsqueda, la zona o el filtro «Solo en uso»."),
            Style = Kit.Style("Muted"),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return panel;
    }

    private Border BuildTile(StationSummaryDto station, decimal rate)
    {
        var statusColor = Labels.StatusColor(station.Status);
        var accent = ColorFrom(statusColor);
        var overtime = station.Overtime;
        var scale = AppearanceService.Current.StationCardScale / 100.0;

        var full = _stations.FirstOrDefault(s => s.Id == station.Id);
        var kind = full?.Kind ?? StationKind.Pc;
        var detail = Labels.StationKindName(kind);
        if (!string.IsNullOrWhiteSpace(full?.Platform)) detail += $" · {full!.Platform}";
        if (kind == StationKind.Console && (full?.Controllers ?? 0) > 0)
            detail += $" · {full!.Controllers} mandos";
        detail += $" · {station.ZoneName ?? "Sin zona"}";

        // Tarjeta rectangular y ancha: las filas Auto/*/Auto garantizan que el
        // contenido nunca se monte sobre la fila siguiente, incluso al 160 %.
        var border = new Border
        {
            Width = Math.Round(320 * scale),
            Height = Math.Round(140 * scale),
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(Math.Round(12 * scale)),
            CornerRadius = new CornerRadius(10 * scale),
            Background = Kit.Brush("SurfaceBrush"),
            BorderBrush = overtime ? Kit.Brush("DangerBrush") : new SolidColorBrush(accent),
            BorderThickness = new Thickness(overtime ? 2 : 1),
            ClipToBounds = true,
            Cursor = Cursors.Hand,
            DataContext = station,
            ToolTip = "Clic para gestionar este equipo"
        };
        border.MouseLeftButtonUp += Station_Click;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ---------- Encabezado: nombre, detalle y estado ----------
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameStack = new StackPanel { Margin = new Thickness(0, 0, 8 * scale, 0) };
        nameStack.Children.Add(new TextBlock
        {
            Text = station.Name,
            FontSize = 15 * scale,
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = detail,
            Style = Kit.Style("Muted"),
            FontSize = 10.5 * scale,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        });
        header.Children.Add(nameStack);

        var pill = Pill(Labels.StatusName(station.Status), statusColor, scale);
        pill.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(pill, 1);
        header.Children.Add(pill);
        grid.Children.Add(header);

        // ---------- Cuerpo: tiempo/estado a la izquierda, importe a la derecha ----------
        var middle = new Grid { Margin = new Thickness(0, 6 * scale, 0, 4 * scale) };
        middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        middle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var readings = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var amounts = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10 * scale, 0, 0, 0)
        };

        if (station.SessionId is not null)
        {
            var prepaid = station.SessionType == SessionType.Prepaid;
            var clock = prepaid
                ? overtime ? $"+{Math.Abs(station.RemainingMinutes)} min" : $"{Math.Max(station.RemainingMinutes, 0)} min"
                : $"{station.ElapsedMinutes} min";

            readings.Children.Add(new TextBlock
            {
                Text = $"{Labels.SessionTypeName(station.SessionType ?? SessionType.Postpaid)} · sesión #{station.SessionId}",
                Style = Kit.Style("Muted"),
                FontSize = 10.5 * scale,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            });
            readings.Children.Add(new TextBlock
            {
                Text = clock,
                FontSize = 24 * scale,
                FontWeight = FontWeights.Bold,
                Foreground = overtime
                    ? Kit.Brush("DangerBrush")
                    : prepaid ? Kit.Brush("AccentBrush") : Kit.Brush("TextBrush")
            });
            readings.Children.Add(new TextBlock
            {
                Text = prepaid ? "tiempo restante" : "tiempo transcurrido",
                Style = Kit.Style("Muted"),
                FontSize = 9.5 * scale
            });

            amounts.Children.Add(new TextBlock
            {
                Text = $"$ {station.CurrentAmount:N2}",
                FontWeight = FontWeights.Bold,
                FontSize = 15 * scale,
                Foreground = Kit.Brush("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            if (rate > 0)
            {
                amounts.Children.Add(new TextBlock
                {
                    Text = $"Bs {station.CurrentAmount * rate:N2}",
                    Style = Kit.Style("Muted"),
                    FontSize = 10 * scale,
                    HorizontalAlignment = HorizontalAlignment.Right
                });
            }
        }
        else
        {
            var (statusText, timeText, timeHint) = station.Status switch
            {
                StationStatus.Maintenance => ("En mantenimiento", "—", "fuera de servicio"),
                StationStatus.Offline => ("Sin conexión", "—", "equipo apagado"),
                _ => ("Equipo libre", "00:00", "sin sesión activa")
            };

            readings.Children.Add(new TextBlock
            {
                Text = statusText,
                FontSize = 12 * scale,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accent),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            });
            readings.Children.Add(new TextBlock
            {
                Text = timeText,
                FontSize = 24 * scale,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accent)
            });
            readings.Children.Add(new TextBlock
            {
                Text = timeHint,
                Style = Kit.Style("Muted"),
                FontSize = 9.5 * scale,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            });

            amounts.Children.Add(new TextBlock
            {
                Text = "sin cobro",
                Style = Kit.Style("Muted"),
                FontSize = 10 * scale,
                HorizontalAlignment = HorizontalAlignment.Right
            });
        }

        middle.Children.Add(readings);
        Grid.SetColumn(amounts, 1);
        middle.Children.Add(amounts);
        Grid.SetRow(middle, 1);
        grid.Children.Add(middle);

        // ---------- Pie: acción ----------
        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        footer.Children.Add(new TextBlock
        {
            Text = station.SessionId is null ? "Disponible" : "Sesión en curso",
            Style = Kit.Style("Muted"),
            FontSize = 9.5 * scale,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        });

        var manage = new TextBlock
        {
            Text = "Gestionar ›",
            FontSize = 11 * scale,
            FontWeight = FontWeights.SemiBold,
            Foreground = Kit.Brush("MutedBrush"),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(manage, 1);
        footer.Children.Add(manage);

        Grid.SetRow(footer, 2);
        grid.Children.Add(footer);

        border.Child = grid;
        return border;
    }

    private static Border Pill(string text, string color, double scale = 1)
    {
        var accent = ColorFrom(color);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(38, accent.R, accent.G, accent.B)),
            CornerRadius = new CornerRadius(6 * scale),
            Padding = new Thickness(8 * scale, 2 * scale, 8 * scale, 2 * scale),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10 * scale,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accent)
            }
        };
    }

    private static Color ColorFrom(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Gray; }
    }

    private async void Station_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StationSummaryDto station }) return;

        var dialog = new StationActionsDialog(station, _dashboard?.ExchangeRate ?? 0,
            systemActions: App.Session.Role != UserRole.Cashier)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Action is null) return;
        await HandleActionAsync(dialog.Action, station);
    }

    /// <summary>Consulta si hay actualizaciones disponibles.</summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await App.Api.LatestUpdateAsync(refresh: false);
            if (update is null) return;

            // Solo mostramos información en el modo administrador, no al cajero
            if (App.Session.Role == UserRole.Cashier) return;

            // Registramos el aviso en la bitácora interna; el banner principal
            // está en MainWindow y se actualiza por ahí.
            Ui.Info($"{L.T("Hay una versión nueva disponible:")} {update.LatestVersion} " +
                $"(actual: {update.CurrentVersion})", "Actualización");
        }
        catch
        {
            // Errores silenciosos: no afectar la UI si no se puede consultar GitHub.
        }
    }

    private async Task HandleActionAsync(string action, StationSummaryDto station)
    {
        var owner = Window.GetWindow(this);

        await Ui.Run(async () =>
        {
            switch (action)
            {
                case "start":
                {
                    var full = _stations.FirstOrDefault(s => s.Id == station.Id);
                    if (full is null) return;

                    var dialog = new StartSessionDialog(full,
                        _rates.Where(r => r.Active).ToList(), _customers,
                        _packages.Where(p => p.Active).ToList())
                    { Owner = owner };

                    if (dialog.ShowDialog() == true && dialog.Result is not null)
                        await App.Api.StartSessionAsync(dialog.Result);
                    break;
                }

                case "stop":
                {
                    var session = _sessions.FirstOrDefault(s => s.StationId == station.Id);
                    if (session is null)
                    {
                        Ui.Info("La sesión ya no está activa.", "Sesión finalizada");
                        break;
                    }

                    var cashOpen = false;
                    try
                    {
                        var cash = await App.Api.CashCurrentAsync();
                        cashOpen = cash.IsOpen;
                    }
                    catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                    {
                        cashOpen = false;
                    }

                    var dialog = new StopSessionDialog(session, _dashboard?.ExchangeRate ?? 0, cashOpen)
                    { Owner = owner };

                    if (dialog.ShowDialog() == true && dialog.Result is not null)
                        await App.Api.StopSessionAsync(session.Id, dialog.Result);
                    break;
                }

                case "extend":
                {
                    var session = _sessions.FirstOrDefault(s => s.StationId == station.Id);
                    if (session is null) break;

                    var dialog = new ExtendSessionDialog(session) { Owner = owner };
                    if (dialog.ShowDialog() == true && dialog.Result is not null)
                        await App.Api.ExtendSessionAsync(session.Id, dialog.Result);
                    break;
                }

                case "pause":
                {
                    var session = _sessions.FirstOrDefault(s => s.StationId == station.Id);
                    if (session is null) break;
                    await App.Api.PauseSessionAsync(session.Id, session.Status != SessionStatus.Paused);
                    break;
                }

                case "transfer":
                {
                    var session = _sessions.FirstOrDefault(s => s.StationId == station.Id);
                    if (session is null) break;

                    var targets = _stations
                        .Where(s => s.Id != station.Id && s.Status == StationStatus.Available && s.Active)
                        .ToList();

                    var dialog = new TransferSessionDialog(station.Name, targets) { Owner = owner };
                    if (dialog.ShowDialog() == true && dialog.TargetStationId is int target)
                        await App.Api.TransferSessionAsync(session.Id, target);
                    break;
                }

                case "screen":
                {
                    var viewer = new ScreenViewerWindow(station.Id, station.Name) { Owner = owner };
                    viewer.Show();
                    break;
                }

                case "wake":
                {
                    if (!Ui.Confirm($"¿Enviar un paquete Wake on LAN para encender {station.Name}?"))
                        break;
                    await App.Api.WakeStationAsync(station.Id);
                    Ui.Info($"Se envió el paquete Wake on LAN a {station.Name}. " +
                            "El equipo debe tener la función activada en la tarjeta de red.",
                        "Encendido remoto");
                    break;
                }

                case "maintenance":
                {
                    if (station.SessionId is not null)
                    {
                        Ui.Info("Cierre la sesión antes de poner el equipo en mantenimiento.",
                            "Equipo con sesión activa");
                        break;
                    }

                    var enable = station.Status != StationStatus.Maintenance;
                    if (!Ui.Confirm(enable
                            ? $"¿Poner {station.Name} en mantenimiento? El equipo quedará bloqueado."
                            : $"¿Habilitar {station.Name} nuevamente?"))
                        break;

                    await App.Api.MaintenanceAsync(station.Id, enable);
                    break;
                }

                case "lock":
                {
                    if (!Ui.Confirm($"¿Bloquear {station.Name} ahora?")) break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.Lock);
                    Ui.Info($"Se envió la orden de bloqueo a {station.Name}.", "Orden enviada");
                    break;
                }

                case "message":
                {
                    var prompt = new PromptDialog("Enviar mensaje", "Mensaje para el cliente",
                        "Su tiempo está por finalizar. Pase por caja, por favor.",
                        $"Se mostrará en la pantalla de {station.Name}.", multiline: true)
                    { Owner = owner };

                    if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Result)) break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.Message, prompt.Result);
                    break;
                }

                case "volume":
                {
                    var dialog = new VolumeDialog(station.Name) { Owner = owner };
                    if (dialog.ShowDialog() != true) break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.SetVolume, dialog.Volume.ToString());
                    Ui.Info($"Volumen de {station.Name} ajustado a {dialog.Volume} %.", "Volumen");
                    break;
                }

                case "mute":
                    await App.Api.CommandAsync(station.Id, AgentCommandType.Mute);
                    Ui.Info($"{station.Name} quedó silenciado.", "Audio");
                    break;

                case "unmute":
                    await App.Api.CommandAsync(station.Id, AgentCommandType.Unmute);
                    Ui.Info($"Audio activado en {station.Name}.", "Audio");
                    break;

                case "monitoroff":
                {
                    if (!Ui.Confirm($"¿Apagar el monitor de {station.Name}? Se encenderá al enviar la orden inversa o al usar el teclado.",
                            "Apagar monitor"))
                        break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.MonitorOff);
                    break;
                }

                case "monitoron":
                    await App.Api.CommandAsync(station.Id, AgentCommandType.MonitorOn);
                    break;

                case "lockinput":
                {
                    if (!Ui.Confirm($"¿Bloquear teclado y mouse de {station.Name}? El cliente no podrá usar el equipo hasta que desbloquee la entrada.",
                            "Bloquear entrada"))
                        break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.LockInput);
                    Ui.Info("Entrada bloqueada. Use «Desbloquear entrada» para liberar el equipo.", "Entrada");
                    break;
                }

                case "unlockinput":
                    await App.Api.CommandAsync(station.Id, AgentCommandType.UnlockInput);
                    Ui.Info($"Entrada desbloqueada en {station.Name}.", "Entrada");
                    break;

                case "killprocess":
                {
                    var prompt = new PromptDialog("Cerrar programa", "Nombre del proceso (ej. chrome)",
                        "chrome", $"Se cerrarán todas las ventanas del proceso en {station.Name}.", multiline: false)
                    { Owner = owner };

                    if (prompt.ShowDialog() != true || string.IsNullOrWhiteSpace(prompt.Result)) break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.KillProcess, prompt.Result);
                    Ui.Info($"Orden de cierre enviada para «{prompt.Result}».", "Cerrar programa");
                    break;
                }

                case "shutdown":
                {
                    if (!Ui.Confirm($"¿Apagar {station.Name}? Se cerrará la sesión de Windows.",
                            "Apagar equipo"))
                        break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.Shutdown);
                    Ui.Info("Orden de apagado enviada.", "Equipo");
                    break;
                }

                case "restart":
                {
                    if (!Ui.Confirm($"¿Reiniciar {station.Name}?", "Reiniciar equipo")) break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.Restart);
                    Ui.Info("Orden de reinicio enviada.", "Equipo");
                    break;
                }

                case "logoff":
                {
                    if (!Ui.Confirm($"¿Cerrar la sesión de Windows de {station.Name}?",
                            "Cerrar sesión de Windows"))
                        break;
                    await App.Api.CommandAsync(station.Id, AgentCommandType.Logoff);
                    Ui.Info("Orden de cierre de sesión enviada.", "Equipo");
                    break;
                }
            }
        }, "No se pudo ejecutar la acción");

        await RefreshAsync();
    }
}
