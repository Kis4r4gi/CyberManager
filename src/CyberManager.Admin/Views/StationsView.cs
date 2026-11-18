using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Desktop.Api;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>Administración de equipos, zonas, tarifas y credenciales de conexión.</summary>
public sealed class StationsView : UserControl, IRefreshable
{
    private readonly DataGrid _grid;
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _statusFilter = new();
    private readonly TextBlock _summaryText = Kit.Muted("Cargando equipos...");
    private readonly TextBlock _clientStatusText = Kit.Muted("Consultando el paquete de cliente...");
    private readonly ServerAddressBar _serverAddressBar = new();

    private List<StationDto> _stations = new();
    private List<ZoneDto> _zones = new();
    private List<RateDto> _rates = new();

    // La lista se refresca sola: el servidor no empuja la lista completa de
    // equipos, solo los cambios de estado, así que se recarga al recibir un
    // evento y además cada 20 s para no quedarse con datos viejos.
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _autoRefresh = new() { Interval = TimeSpan.FromSeconds(20) };

    public StationsView()
    {
        _grid = Kit.Table(
            Kit.AutoCol("Equipo", nameof(StationRow.Name), 140),
            Kit.AutoCol("Tipo", nameof(StationRow.Kind), 80),
            Kit.Col("Mandos", nameof(StationRow.Controllers), 75),
            Kit.AutoCol("Zona", nameof(StationRow.Zone), 110),
            Kit.AutoCol("Dirección IP", nameof(StationRow.Ip), 120),
            Kit.AutoCol("Tarifa", nameof(StationRow.Rate), 130),
            Kit.Col("USD/hora", nameof(StationRow.RatePerHour), 95, "{0:N2}"),
            Kit.TemplateCol("Estado", Kit.Pill(nameof(StationRow.Status), nameof(StationRow.StatusBrush)), 130),
            Kit.AutoCol("Última conexión", nameof(StationRow.LastSeen), 140),
            Kit.Col("Activo", nameof(StationRow.Active), 75),
            ActionsColumn());

        // La lista de equipos se muestra sin el marco grande: sin borde, sin fondo
        // sólido y sin franjas alternas. Cada columna se ajusta al texto de su
        // contenido y de su encabezado, y el usuario puede arrastrar el borde para
        // cambiar el ancho cuando lo necesite.
        _grid.Background = Brushes.Transparent;
        _grid.RowBackground = Brushes.Transparent;
        _grid.AlternatingRowBackground = Brushes.Transparent;
        _grid.BorderThickness = new Thickness(0);
        _grid.ColumnWidth = DataGridLength.Auto;
        _grid.CanUserResizeColumns = true;
        foreach (var column in _grid.Columns)
        {
            column.MinWidth = 0;
            column.Width = DataGridLength.Auto;
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(Kit.PageHeader("Equipos y zonas",
            "Administre los PC del local, su tarifa por hora y las credenciales que usa el cliente.",
            Kit.Button("Escanear la red", "PrimaryButton", ScanNetwork),
            Kit.Button("Nuevo equipo", "PrimaryButton", NewStation),
            Kit.Button("Actualizar", "GhostButton", () => _ = Ui.Run(RefreshAsync, "No se pudieron cargar los equipos"))));

        // ---- Box de actualización de los clientes instalados ----
        var clientWrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        clientWrap.Children.Add(new TextBlock
        {
            Text = L.T("Clientes"),
            Style = Kit.Style("H2"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 8)
        });

        _clientStatusText.VerticalAlignment = VerticalAlignment.Center;
        _clientStatusText.Margin = new Thickness(0, 0, 14, 8);
        clientWrap.Children.Add(_clientStatusText);

        clientWrap.Children.Add(_serverAddressBar);

        var updateButton = Kit.Button("Actualizar clientes", "BlueButton", UpdateClients);
        updateButton.Margin = new Thickness(0, 0, 8, 8);
        clientWrap.Children.Add(updateButton);

        var reviewButton = Kit.Button("Revisar", "GhostButton",
            () => _ = Ui.Run(RefreshClientUpdateAsync, "No se pudo consultar el paquete de cliente"));
        reviewButton.Margin = new Thickness(0, 0, 0, 8);
        clientWrap.Children.Add(reviewButton);

        var clientBox = new Border
        {
            Style = Kit.Style("Card"),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
            Child = clientWrap
        };
        Grid.SetRow(clientBox, 1);
        root.Children.Add(clientBox);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ---- Fila 1: buscador a la izquierda, resumen a la derecha ----
        var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _searchBox.Width = 200;
        _searchBox.VerticalAlignment = VerticalAlignment.Center;
        _searchBox.ToolTip = "Buscar equipo por nombre, IP o zona";
        _searchBox.TextChanged += (_, _) => Render();

        // Menú desplegable de estado junto al buscador: sustituye los boxes de métricas.
        _statusFilter.ItemsSource = new[]
        {
            new StatusOption("Todos los estados", null),
            new StatusOption("En línea", s => StationPresence.IsOnline(s.LastSeenUtc)),
            new StatusOption("Disponibles", s => s.Status == StationStatus.Available),
            new StatusOption("En uso", s => s.Status == StationStatus.InUse),
            new StatusOption("En pausa", s => s.Status == StationStatus.Paused),
            new StatusOption("Mantenimiento", s => s.Status == StationStatus.Maintenance),
            new StatusOption("Desconectados", s => !StationPresence.IsOnline(s.LastSeenUtc))
        };
        _statusFilter.SelectedIndex = 0;
        _statusFilter.Width = 160;
        _statusFilter.Margin = new Thickness(10, 0, 0, 0);
        _statusFilter.VerticalAlignment = VerticalAlignment.Center;
        _statusFilter.ToolTip = "Filtra la lista por el estado del equipo";
        _statusFilter.SelectionChanged += (_, _) => Render();

        var search = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        search.Children.Add(new TextBlock
        {
            Text = L.T("Buscar equipo"),
            Style = Kit.Style("Label"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        search.Children.Add(_searchBox);
        search.Children.Add(_statusFilter);
        searchRow.Children.Add(search);

        _summaryText.VerticalAlignment = VerticalAlignment.Center;
        _summaryText.HorizontalAlignment = HorizontalAlignment.Right;
        _summaryText.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(_summaryText, 1);
        searchRow.Children.Add(_summaryText);

        content.Children.Add(searchRow);

        // Las acciones se abren desde el menú desplegable de cada equipo; ya no hay
        // una fila de botones que dependa de la selección de la lista.
        Grid.SetRow(_grid, 1);
        content.Children.Add(_grid);

        Grid.SetRow(content, 2);
        root.Children.Add(content);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        var hint = Kit.Muted("Abra el menú «Acciones» de cada equipo para subir, bajar, editar, cambiar el nombre o la conexión, bloquear, enviar mensajes o eliminarlo. Cada equipo necesita su ID y token; use la IP LAN del servidor, no localhost.");
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        footer.Children.Add(hint);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;

        _grid.MouseDoubleClick += (_, _) => EditStation();

        if (App.Session.Role == UserRole.Admin)
        {
            _grid.ContextMenu = BuildStationMenu();
            _grid.PreviewMouseRightButtonDown += OnGridRightButtonDown;
        }

        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RefreshSilentAsync();
        };
        _autoRefresh.Tick += async (_, _) => await RefreshSilentAsync();

        Loaded += async (_, _) =>
        {
            Subscribe();
            _autoRefresh.Start();
            await Ui.Run(RefreshAsync, "No se pudieron cargar los equipos");
        };
        Unloaded += (_, _) =>
        {
            Unsubscribe();
            _autoRefresh.Stop();
        };
    }

    private void Subscribe()
    {
        if (App.Realtime is null) return;
        Unsubscribe();
        App.Realtime.StationStateChanged += OnStationState;
        App.Realtime.EventReceived += OnRealtimeEvent;
    }

    private void Unsubscribe()
    {
        if (App.Realtime is null) return;
        App.Realtime.StationStateChanged -= OnStationState;
        App.Realtime.EventReceived -= OnRealtimeEvent;
    }

    private void OnStationState(StationStateEvent e) => Dispatcher.BeginInvoke(() =>
    {
        _debounce.Stop();
        _debounce.Start();
    });

    private void OnRealtimeEvent(RealtimeEvent e)
    {
        if (e.Type is not ("StationOnline" or "StationOffline" or "AgentOnline" or "AgentOffline"
            or "SessionStarted" or "SessionEnded" or "StationMaintenance")) return;

        Dispatcher.BeginInvoke(() =>
        {
            _debounce.Stop();
            _debounce.Start();
        });
    }

    private async Task RefreshSilentAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch
        {
            // El servidor puede estar reiniciando; se reintenta en el próximo ciclo.
        }
    }

    private StationDto? Selected => (_grid.SelectedItem as StationRow)?.Source;

    /// <summary>Columna con el menú desplegable de acciones de cada equipo.</summary>
    private DataGridTemplateColumn ActionsColumn()
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetValue(ContentControl.ContentProperty, L.T("Acciones ▾"));
        button.SetValue(FrameworkElement.StyleProperty, Kit.Style("GhostButton"));
        button.SetValue(Control.PaddingProperty, new Thickness(10, 3, 10, 3));
        button.SetValue(Control.FontSizeProperty, 11.0);
        button.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        button.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4));
        button.SetValue(FrameworkElement.ToolTipProperty,
            L.T("Subir, bajar, editar, cambiar nombre o conexión, bloquear, enviar mensajes o eliminar este equipo."));
        button.AddHandler(Button.ClickEvent, new RoutedEventHandler(OpenRowMenu));

        return Kit.TemplateCol("Acciones", button, 110);
    }

    /// <summary>Abre el menú completo del equipo sobre el botón de su fila.</summary>
    private void OpenRowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StationRow row } button) return;

        _grid.SelectedItem = row;
        var menu = BuildStationMenu();
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>Menú con todas las acciones del equipo; se usa en la fila y con clic derecho.</summary>
    private ContextMenu BuildStationMenu()
    {
        var menu = new ContextMenu
        {
            Background = Kit.Brush("SurfaceBrush"),
            Foreground = Kit.Brush("TextBrush"),
            BorderBrush = Kit.Brush("BorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        menu.Items.Add(StationMenuItem("Subir", () => MoveStation(-1)));
        menu.Items.Add(StationMenuItem("Bajar", () => MoveStation(1)));
        menu.Items.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

        menu.Items.Add(StationMenuItem("Editar", EditStation));
        menu.Items.Add(StationMenuItem("Cambiar nombre", RenameStation));
        menu.Items.Add(StationMenuItem("Editar conexión", EditConnection));
        menu.Items.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

        menu.Items.Add(StationMenuItem("Configuración / token", ShowConfig));
        menu.Items.Add(StationMenuItem("Ver pantalla", ShowScreen));
        menu.Items.Add(StationMenuItem("Bloquear", LockStation));
        menu.Items.Add(StationMenuItem("Mensaje", SendMessage));
        menu.Items.Add(StationMenuItem("Actualizar cliente", UpdateClient));
        menu.Items.Add(StationMenuItem("Mantenimiento", ToggleMaintenance));
        menu.Items.Add(StationMenuItem("Activar / desactivar", ToggleActive));
        menu.Items.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

        menu.Items.Add(StationMenuItem("Eliminar equipo", DeleteStation, danger: true));

        return menu;
    }

    private static MenuItem StationMenuItem(string header, Action click, bool danger = false)
    {
        var item = new MenuItem
        {
            Header = L.T(header),
            Foreground = Kit.Brush(danger ? "DangerBrush" : "TextBrush"),
            Background = Brushes.Transparent,
            Padding = new Thickness(12, 7, 12, 7),
            FontSize = 12.5
        };
        item.Click += (_, _) => click();
        return item;
    }

    /// <summary>Selecciona la fila sobre la que se hizo clic derecho antes de abrir el menú.</summary>
    private void OnGridRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not { Item: StationRow row })
            return;

        _grid.SelectedItem = row;
        _grid.CurrentCell = new DataGridCellInfo(row, _grid.Columns[0]);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null && source is not T)
            source = VisualTreeHelper.GetParent(source);

        return source as T;
    }

    public async Task RefreshAsync()
    {
        var stationsTask = App.Api.StationsAsync();
        var zonesTask = App.Api.ZonesAsync();
        var ratesTask = App.Api.RatesAsync();

        await Task.WhenAll(stationsTask, zonesTask, ratesTask);

        _stations = await stationsTask;
        _zones = await zonesTask;
        _rates = await ratesTask;

        Render();

        // La IP del servidor se consulta aparte: si falla, la barra muestra la
        // dirección de la configuración y el resto de la pantalla sigue cargando.
        try { await _serverAddressBar.RefreshAsync(); }
        catch { /* se reintenta en el próximo refresco */ }

        await RefreshClientUpdateAsync();
    }

    private void Render()
    {
        var term = _searchBox.Text.Trim();
        var status = (_statusFilter.SelectedItem as StatusOption)?.Match;
        var selectedId = Selected?.Id;

        var filtered = _stations
            .Where(s => status is null || status(s))
            .Where(s => term.Length == 0
                        || s.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (s.IpAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (s.ZoneName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (s.RateName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (s.Platform?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                        || Labels.StationKindName(s.Kind).Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToList();

        _grid.ItemsSource = filtered.Select(s => new StationRow(s)).ToList();

        // La lista se refresca sola; se conserva la fila seleccionada para que el
        // menú de acciones siga apuntando al mismo equipo.
        if (selectedId is int id)
        {
            var row = _grid.Items.OfType<StationRow>().FirstOrDefault(r => r.Source.Id == id);
            if (row is not null) _grid.SelectedItem = row;
        }

        var online = _stations.Count(s => StationPresence.IsOnline(s.LastSeenUtc));

        _summaryText.Text = $"{filtered.Count} equipos visibles · {online} en línea · " +
                            $"{_stations.Count(s => s.Status == StationStatus.Paused)} en pausa";
    }

    /// <summary>Mueve el equipo seleccionado una posición y guarda el orden manual.</summary>
    private void MoveStation(int delta)
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var ordered = _stations
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToList();

        var index = ordered.FindIndex(s => s.Id == station.Id);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= ordered.Count) return;

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        var ids = ordered.Select(s => s.Id).ToList();

        _ = Ui.Run(async () =>
        {
            await App.Api.ReorderStationsAsync(ids);
            await RefreshAsync();
        }, "No se pudo guardar el orden de los equipos");
    }

    private void NewStation()
    {
        var dialog = new StationDialog(_zones, _rates) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveStationAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo crear el equipo");
    }

    private void EditStation()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var dialog = new StationDialog(_zones, _rates, station) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveStationAsync(station.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el equipo");
    }

    /// <summary>Cambia solo el nombre visible del equipo.</summary>
    private void RenameStation()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var dialog = new PromptDialog("Cambiar nombre", "Nuevo nombre del equipo",
            station.Name, "El nombre se muestra en el tablero y en la pantalla del cliente.")
        { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Result)) return;

        var newName = dialog.Result.Trim();
        if (string.Equals(newName, station.Name, StringComparison.Ordinal)) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveStationAsync(station.Id,
                ToSaveRequest(station, newName, station.IpAddress, station.MacAddress));
            await RefreshAsync();
        }, "No se pudo cambiar el nombre del equipo");
    }

    /// <summary>Edita la dirección IP y la MAC del equipo seleccionado.</summary>
    private void EditConnection()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var dialog = new StationConnectionDialog(station) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveStationAsync(station.Id,
                ToSaveRequest(station, station.Name, dialog.IpAddress, dialog.MacAddress));
            await RefreshAsync();
        }, "No se pudo guardar la conexión del equipo");
    }

    private static SaveStationRequest ToSaveRequest(StationDto station, string name,
        string? ip, string? mac)
        => new(name, ip, mac, station.ZoneId, station.RateId, station.Notes, station.SortOrder,
            station.Active, station.Kind, station.Platform, station.Controllers);

    private void ShowConfig()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var owner = Window.GetWindow(this);

        _ = Ui.Run(async () =>
        {
            var config = await App.Api.StationConfigAsync(station.Id);
            var dialog = new StationTokenDialog(config, async () =>
            {
                await App.Api.RegenerateTokenAsync(station.Id);
                return await App.Api.StationConfigAsync(station.Id);
            })
            { Owner = owner };
            dialog.ShowDialog();
        }, "No se pudo obtener la configuración del equipo");
    }

    private void ToggleMaintenance()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var enable = station.Status != StationStatus.Maintenance;

        if (enable && station.CurrentSessionId is not null)
        {
            Ui.Info("Cierre la sesión activa antes de poner el equipo en mantenimiento.", "Equipo en uso");
            return;
        }

        if (!Ui.Confirm(enable
                ? $"¿Poner {station.Name} en mantenimiento? El equipo quedará bloqueado."
                : $"¿Habilitar {station.Name} nuevamente?", "Mantenimiento"))
            return;

        _ = Ui.Run(async () =>
        {
            await App.Api.MaintenanceAsync(station.Id, enable);
            await RefreshAsync();
        }, "No se pudo cambiar el mantenimiento");
    }

    private void LockStation()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        if (!Ui.Confirm($"¿Bloquear {station.Name} ahora?", "Bloquear equipo")) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.CommandAsync(station.Id, AgentCommandType.Lock);
            Ui.Info($"Se envió la orden de bloqueo a {station.Name}.", "Orden enviada");
        }, "No se pudo enviar la orden");
    }

    private void ShowScreen()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Visualizador remoto");
            return;
        }

        var viewer = new ScreenViewerWindow(station.Id, station.Name) { Owner = Window.GetWindow(this) };
        viewer.Show();
    }

    private void SendMessage()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var dialog = new PromptDialog("Enviar mensaje", "Mensaje para el cliente",
            "Su tiempo está por finalizar. Pase por caja, por favor.",
            $"Se mostrará en la pantalla de {station.Name}.", multiline: true)
        { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Result)) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.CommandAsync(station.Id, AgentCommandType.Message, dialog.Result);
            Ui.Info($"Mensaje enviado a {station.Name}.", "Orden enviada");
        }, "No se pudo enviar el mensaje");
    }

    private void UpdateClient()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        if (!Ui.Confirm($"¿Actualizar el cliente de {station.Name} a la última versión publicada?",
                "Actualizar cliente"))
            return;

        _ = Ui.Run(async () =>
        {
            await App.Api.NotifyClientUpdateAsync(station.Id);
            await RefreshClientUpdateAsync();
            Ui.Info($"Se envió el aviso de actualización a {station.Name}. " +
                    "El cliente descargará la versión nueva y se reiniciará al terminar.",
                "Actualizar cliente");
        }, "No se pudo actualizar el cliente");
    }

    private void ToggleActive()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        var enable = !station.Active;
        if (!Ui.Confirm(enable
                ? $"¿Reactivar {station.Name}?"
                : $"¿Desactivar {station.Name}? No aparecerá en el tablero hasta reactivarlo.",
                "Equipo"))
            return;

        _ = Ui.Run(async () =>
        {
            var request = new SaveStationRequest(station.Name, station.IpAddress, station.MacAddress,
                station.ZoneId, station.RateId, station.Notes, station.SortOrder, enable,
                station.Kind, station.Platform, station.Controllers);
            await App.Api.SaveStationAsync(station.Id, request);
            await RefreshAsync();
        }, "No se pudo actualizar el equipo");
    }

    private async void DeleteStation()
    {
        if (Selected is not { } station)
        {
            Ui.Info("Seleccione un equipo de la lista.", "Equipos");
            return;
        }

        if (await App.Api.StationHasActiveSessionsAsync(station.Id))
        {
            Ui.Info("No se puede eliminar el equipo mientras tiene sesiones activas.", "Equipo");
            return;
        }

        if (!Ui.Confirm(
                $"¿Eliminar permanentemente el equipo {station.Name}? " +
                "Esto desactivará el equipo y limpiará sus referencias.",
                "Eliminar equipo"))
            return;

        _ = Ui.Run(async () =>
        {
            await App.Api.DeactivateStationAsync(station.Id);
            await RefreshAsync();
        }, "No se pudo desactivar el equipo");
    }

    // ---- escaneo de la red y actualización de los clientes ----

    /// <summary>Busca estaciones instaladas en la red y permite darlas de alta.</summary>
    private void ScanNetwork()
    {
        _ = Ui.Run(async () =>
        {
            var found = await App.Api.ScanNetworkAsync();
            if (found.Count == 0)
            {
                Ui.Info("No se encontró ningún equipo en la red local. Verifique que las " +
                        "estaciones estén encendidas y que el firewall permita los puertos " +
                        "UDP 5092 y 5093.", "Escanear la red");
                return;
            }

            var dialog = new NetworkScanDialog(found) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true || dialog.Selected.Count == 0) return;

            var result = await App.Api.ApplyDiscoveryAsync(dialog.Selected);
            await RefreshAsync();

            var detail = result.Messages.Count > 0
                ? "\n\n" + string.Join("\n", result.Messages)
                : string.Empty;
            Ui.Info($"{result.Created} equipos nuevos, {result.Updated} actualizados y " +
                    $"{result.Configured} configurados en la red.{detail}", "Escanear la red");
        }, "No se pudo escanear la red");
    }

    /// <summary>Avisa a las estaciones y agentes que hay un paquete de cliente nuevo.</summary>
    private void UpdateClients()
    {
        _ = Ui.Run(async () =>
        {
            await App.Api.NotifyClientUpdateAsync();
            await RefreshClientUpdateAsync();
            Ui.Info("Se avisó a las estaciones y agentes. Los equipos descargarán la versión " +
                    "nueva y se reiniciarán automáticamente.", "Actualizar clientes");
        }, "No se pudo avisar a los clientes");
    }

    private async Task RefreshClientUpdateAsync()
    {
        try
        {
            var status = await App.Api.ClientUpdateStatusAsync();
            _clientStatusText.Text = status.HasPackage
                ? $"Paquete {status.PackageVersion} · {status.UpToDate} al día · " +
                  $"{status.Outdated} pendientes · {status.Unknown} sin versión"
                : "Sin paquete de cliente. Publique la versión con el instalador del servidor.";
            _clientStatusText.ToolTip = status.Message;
        }
        catch
        {
            _clientStatusText.Text = "No se pudo consultar el estado de los clientes.";
        }
    }

    /// <summary>Opción del menú de estados junto al buscador.</summary>
    private sealed record StatusOption(string Label, Func<StationDto, bool>? Match)
    {
        public override string ToString() => L.T(Label);
    }
}

public sealed class StationRow
{
    public StationRow(StationDto station)
    {
        Source = station;
        Name = station.Name;
        Kind = Labels.StationKindName(station.Kind);
        Controllers = station.Kind == StationKind.Console
            ? (station.Controllers ?? 0).ToString()
            : "—";
        Zone = station.ZoneName ?? "Sin zona";
        Ip = string.IsNullOrWhiteSpace(station.IpAddress) ? "—" : station.IpAddress!;
        Rate = station.RateName ?? "Sin tarifa";
        RatePerHour = station.RatePerHour;
        IsOnline = StationPresence.IsOnline(station.LastSeenUtc);

        // La columna «Estado» refleja la presencia: un equipo sin latido se
        // muestra como «Sin conexión» aunque conserve una sesión abierta.
        var displayStatus = IsOnline || station.Status == StationStatus.Maintenance
            ? station.Status
            : StationStatus.Offline;
        Status = Labels.StatusName(displayStatus);
        StatusBrush = Labels.StatusColor(displayStatus);
        LastSeen = station.LastSeenUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
        Active = station.Active ? "Sí" : "No";
    }

    public StationDto Source { get; }
    public string Name { get; }
    public string Kind { get; }
    public string Controllers { get; }
    public string Zone { get; }
    public string Ip { get; }
    public string Rate { get; }
    public decimal RatePerHour { get; }
    public bool IsOnline { get; }
    public string Status { get; }
    public string StatusBrush { get; }
    public string LastSeen { get; }
    public string Active { get; }
}

/// <summary>Edita únicamente la dirección de red del equipo.</summary>
public sealed class StationConnectionDialog : FormDialog
{
    private readonly TextBox _ip;
    private readonly TextBox _mac;

    public string? IpAddress { get; private set; }
    public string? MacAddress { get; private set; }

    public StationConnectionDialog(StationDto station)
        : base($"Conexión de {station.Name}",
            "Dirección de red que usa el equipo para reportar al servidor.")
    {
        _ip = Add(new TextBox { Text = station.IpAddress ?? string.Empty },
            "Dirección IP (opcional)");
        _mac = Add(new TextBox { Text = station.MacAddress ?? string.Empty },
            "Dirección MAC (opcional)");

        AddNote("La MAC se usa para encender el equipo con Wake on LAN. " +
                "El token de vinculación se consulta en «Configuración / token».");
    }

    protected override void OnAccept()
    {
        IpAddress = string.IsNullOrWhiteSpace(_ip.Text) ? null : _ip.Text.Trim();
        MacAddress = string.IsNullOrWhiteSpace(_mac.Text) ? null : _mac.Text.Trim();
        base.OnAccept();
    }
}

public sealed class StationDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly TextBox _ip;
    private readonly TextBox _mac;
    private readonly ComboBox _kindBox;
    private readonly TextBox _platform;
    private readonly TextBox _controllers;
    private readonly ComboBox _zoneBox;
    private readonly ComboBox _rateBox;
    private readonly TextBox _sort;
    private readonly TextBox _notes;
    private readonly CheckBox _active;

    public SaveStationRequest? Result { get; private set; }

    public StationDialog(List<ZoneDto> zones, List<RateDto> rates, StationDto? station = null)
        : base(station is null ? "Nuevo equipo" : $"Editar {station.Name}",
            "El nombre se muestra en el tablero y en la pantalla del cliente.")
    {
        _name = Add(new TextBox { Text = station?.Name ?? string.Empty }, "Nombre del equipo");
        _ip = Add(new TextBox { Text = station?.IpAddress ?? string.Empty }, "Dirección IP (opcional)");
        _mac = Add(new TextBox { Text = station?.MacAddress ?? string.Empty }, "Dirección MAC (opcional)");

        var kindOptions = new[]
        {
            new Option<StationKind>(StationKind.Pc, "Computadora (PC)"),
            new Option<StationKind>(StationKind.Console, "Consola de videojuegos"),
            new Option<StationKind>(StationKind.Other, "Otro equipo")
        };
        _kindBox = AddCombo("Tipo de equipo", kindOptions);
        var currentKind = kindOptions.FirstOrDefault(o => o.Value == (station?.Kind ?? StationKind.Pc));
        if (currentKind is not null) _kindBox.SelectedItem = currentKind;

        _platform = Add(new TextBox { Text = station?.Platform ?? string.Empty },
            "Plataforma (ej. PlayStation 5, Xbox Series X)");
        _controllers = Add(new TextBox { Text = (station?.Controllers ?? 0).ToString() },
            "Cantidad de mandos (consolas)");

        _kindBox.SelectionChanged += (_, _) => UpdateKindFields(station?.Controllers);
        UpdateKindFields(station?.Controllers);

        var zoneOptions = new List<Option<int?>> { new(null, "Sin zona") };
        zoneOptions.AddRange(zones.Where(z => z.Active).OrderBy(z => z.SortOrder).ThenBy(z => z.Name)
            .Select(z => new Option<int?>(z.Id, z.Name)));
        _zoneBox = AddCombo("Zona", zoneOptions);
        SelectOption(_zoneBox, zoneOptions, station?.ZoneId);

        var rateOptions = new List<Option<int?>> { new(null, "Tarifa por defecto") };
        rateOptions.AddRange(rates.Where(r => r.Active).OrderBy(r => r.PricePerHour)
            .Select(r => new Option<int?>(r.Id, r.Name)));
        _rateBox = AddCombo("Tarifa por hora", rateOptions);
        SelectOption(_rateBox, rateOptions, station?.RateId);

        _sort = Add(new TextBox { Text = (station?.SortOrder ?? 0).ToString() }, "Orden en el tablero");
        _notes = Add(new TextBox { Text = station?.Notes ?? string.Empty }, "Notas internas");
        _active = AddCheck("Equipo activo", station?.Active ?? true);
    }

    private void UpdateKindFields(int? controllers)
    {
        var kind = (_kindBox.SelectedItem as Option<StationKind>)?.Value ?? StationKind.Pc;
        _platform.IsEnabled = kind is StationKind.Console or StationKind.Other;
        _controllers.IsEnabled = kind == StationKind.Console;

        if (kind != StationKind.Console && string.IsNullOrWhiteSpace(_controllers.Text))
            _controllers.Text = "0";
        if (controllers is int value && _controllers.Text == "0" && value > 0)
            _controllers.Text = value.ToString();
    }

    private static void SelectOption(ComboBox combo, List<Option<int?>> options, int? value)
    {
        var option = options.FirstOrDefault(o => o.Value == value);
        if (option is not null) combo.SelectedItem = option;
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre del equipo.");

        var zoneId = (_zoneBox.SelectedItem as Option<int?>)?.Value;
        var rateId = (_rateBox.SelectedItem as Option<int?>)?.Value;
        var kind = ((Option<StationKind>)_kindBox.SelectedItem!).Value;

        var controllers = 0;
        if (kind == StationKind.Console)
            controllers = ParseInt(_controllers, "Cantidad de mandos");

        Result = new SaveStationRequest(
            _name.Text.Trim(),
            NullIfEmpty(_ip.Text),
            NullIfEmpty(_mac.Text),
            zoneId,
            rateId,
            NullIfEmpty(_notes.Text),
            ParseInt(_sort, "Orden"),
            _active.IsChecked == true,
            kind,
            NullIfEmpty(_platform.Text),
            kind == StationKind.Console ? controllers : null);

        base.OnAccept();
    }

    private static string? NullIfEmpty(string text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Muestra el ID y el token que debe configurar el equipo cliente.</summary>
public sealed class StationTokenDialog : FormDialog
{
    private readonly TextBox _tokenBox;
    private readonly TextBlock _statusText = new() { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly Func<Task<StationConfigDto>> _regenerate;
    private StationConfigDto _config;

    public StationTokenDialog(StationConfigDto config, Func<Task<StationConfigDto>> regenerate)
        : base($"Conexión de {config.Name}", "Use estos datos en la ventana de configuración del equipo cliente.")
    {
        _config = config;
        _regenerate = regenerate;

        AcceptButton.Content = "Cerrar";
        HideCancel();

        AddRow("ID del equipo", config.StationId.ToString());
        AddRow("Servidor sugerido", config.ServerUrl);

        Body.Children.Add(new TextBlock
        {
            Text = "TOKEN DEL EQUIPO",
            Style = Style("Label"),
            Margin = new Thickness(0, 14, 0, 4)
        });

        _tokenBox = new TextBox
        {
            Text = config.Token,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        Body.Children.Add(_tokenBox);

        var copy = Kit.SmallButton("Copiar token", "GhostButton", () =>
        {
            Clipboard.SetText(_tokenBox.Text);
            _statusText.Text = "Token copiado al portapapeles.";
            _statusText.Foreground = Brush("SuccessBrush");
        });
        copy.HorizontalAlignment = HorizontalAlignment.Left;
        copy.Margin = new Thickness(0, 8, 0, 0);
        Body.Children.Add(copy);

        AddSeparator();

        var regenerateButton = Kit.Button("Regenerar token", "WarningButton", () => _ = RegenerateAsync());
        regenerateButton.Padding = new Thickness(12, 8, 12, 8);
        Body.Children.Add(regenerateButton);

        AddNote("Si regenera el token, el equipo cliente dejará de conectar hasta que actualice su configuración. " +
                "El servidor debe ser accesible por la IP de la red local, por ejemplo http://192.168.1.10:5080.");

        _statusText.Style = Style("Muted");
        Body.Children.Add(_statusText);
    }

    private async Task RegenerateAsync()
    {
        if (!Ui.Confirm($"¿Regenerar el token de {_config.Name}? El equipo deberá reconfigurarse.", "Regenerar token"))
            return;

        try
        {
            _config = await _regenerate();
            _tokenBox.Text = _config.Token;
            _statusText.Text = "Token regenerado. Actualice la configuración del equipo cliente.";
            _statusText.Foreground = Brush("WarningBrush");
        }
        catch (Exception ex)
        {
            _statusText.Text = ex.Message;
            _statusText.Foreground = Brush("DangerBrush");
        }
    }
}
