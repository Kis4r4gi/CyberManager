using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Desktop.Api;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>Administración de equipos, zonas, tarifas y credenciales de conexión.</summary>
public sealed class StationsView : UserControl, IRefreshable
{
    private readonly DataGrid _grid;
    private readonly TextBox _searchBox = new();
    private readonly TextBlock _summaryText = Kit.Muted("Cargando equipos...");
    private readonly TextBlock _totalValue;
    private readonly TextBlock _onlineValue;
    private readonly TextBlock _availableValue;
    private readonly TextBlock _inUseValue;
    private readonly TextBlock _maintenanceValue;

    private List<StationDto> _stations = new();
    private List<ZoneDto> _zones = new();
    private List<RateDto> _rates = new();

    public StationsView()
    {
        _grid = Kit.Table(
            Kit.Col("Equipo", nameof(StationRow.Name), 140),
            Kit.Col("Tipo", nameof(StationRow.Kind), 80),
            Kit.Col("Plataforma", nameof(StationRow.Platform), 130),
            Kit.Col("Mandos", nameof(StationRow.Controllers), 70),
            Kit.Col("Zona", nameof(StationRow.Zone), 110),
            Kit.Col("Dirección IP", nameof(StationRow.Ip), 120),
            Kit.Col("Tarifa", nameof(StationRow.Rate), 150),
            Kit.Col("USD/hora", nameof(StationRow.RatePerHour), 95, "{0:N2}"),
            Kit.TemplateCol("Estado", Kit.Pill(nameof(StationRow.Status), nameof(StationRow.StatusBrush)), 130),
            Kit.Col("Sesión", nameof(StationRow.Session), 90),
            Kit.Col("Última conexión", nameof(StationRow.LastSeen), 145),
            Kit.Col("Usuario Windows", nameof(StationRow.CurrentUser), 140),
            Kit.Col("Activo", nameof(StationRow.Active), 80));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(Kit.PageHeader("Equipos y zonas",
            "Administre los PC del local, su tarifa por hora y las credenciales que usa el cliente.",
            Kit.Button("Nuevo equipo", "PrimaryButton", NewStation),
            Kit.Button("Actualizar", "GhostButton", () => _ = Ui.Run(RefreshAsync, "No se pudieron cargar los equipos"))));

        var metrics = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, 0, 16) };
        metrics.Children.Add(Metric("Equipos registrados", "Accent2Brush", out _totalValue));
        metrics.Children.Add(Metric("En línea", "SuccessBrush", out _onlineValue));
        metrics.Children.Add(Metric("Disponibles", "AccentBrush", out _availableValue));
        metrics.Children.Add(Metric("En uso", "PurpleBrush", out _inUseValue));
        metrics.Children.Add(Metric("Mantenimiento", "WarningBrush", out _maintenanceValue));
        Grid.SetRow(metrics, 1);
        root.Children.Add(metrics);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _searchBox.ToolTip = "Buscar equipo por nombre, IP o zona";
        _searchBox.TextChanged += (_, _) => Render();
        toolbar.Children.Add(_searchBox);

        var actions = Kit.HStack(8,
            Kit.Button("Subir", "GhostButton", () => MoveStation(-1)),
            Kit.Button("Bajar", "GhostButton", () => MoveStation(1)),
            Kit.Button("Editar", "GhostButton", EditStation),
            Kit.Button("Configuración / token", "BlueButton", ShowConfig),
            Kit.Button("Ver pantalla", "BlueButton", ShowScreen),
            Kit.Button("Mantenimiento", "WarningButton", ToggleMaintenance),
            Kit.Button("Bloquear", "GhostButton", LockStation),
            Kit.Button("Mensaje", "GhostButton", SendMessage),
            Kit.Button("Activar / desactivar", "GhostButton", ToggleActive));
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(actions, 1);
        toolbar.Children.Add(actions);

        content.Children.Add(toolbar);

        Grid.SetRow(_grid, 1);
        content.Children.Add(_grid);

        Grid.SetRow(content, 2);
        root.Children.Add(content);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(_summaryText);
        var hint = Kit.Muted("Use «Subir» y «Bajar» para el orden manual. Cada equipo necesita su ID y token; use la IP LAN del servidor, no localhost.");
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        footer.Children.Add(hint);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;

        _grid.MouseDoubleClick += (_, _) => EditStation();

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudieron cargar los equipos");
    }

    private static Border Metric(string label, string accent, out TextBlock valueText)
    {
        var card = Kit.StatCard(label, "—", null, accent, out var value);
        card.Margin = new Thickness(0, 0, 10, 0);
        valueText = value;
        return card;
    }

    private StationDto? Selected => (_grid.SelectedItem as StationRow)?.Source;

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
    }

    private void Render()
    {
        var term = _searchBox.Text.Trim();

        var filtered = _stations
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

        var online = _stations.Count(s => s.Status != StationStatus.Offline);
        var available = _stations.Count(s => s.Status == StationStatus.Available);
        var inUse = _stations.Count(s => s.Status == StationStatus.InUse);
        var maintenance = _stations.Count(s => s.Status == StationStatus.Maintenance);

        _totalValue.Text = _stations.Count.ToString();
        _onlineValue.Text = online.ToString();
        _availableValue.Text = available.ToString();
        _inUseValue.Text = inUse.ToString();
        _maintenanceValue.Text = maintenance.ToString();

        _totalValue.ToolTip = $"{_stations.Count} equipos registrados";
        _onlineValue.ToolTip = "Equipos que han reportado al servidor recientemente";
        _maintenanceValue.ToolTip = $"{_stations.Count(s => s.Status == StationStatus.Paused)} equipos en pausa";

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
}

public sealed class StationRow
{
    public StationRow(StationDto station)
    {
        Source = station;
        Name = station.Name;
        Kind = Labels.StationKindName(station.Kind);
        Platform = string.IsNullOrWhiteSpace(station.Platform) ? "—" : station.Platform!;
        Controllers = station.Kind == StationKind.Console
            ? (station.Controllers ?? 0).ToString()
            : "—";
        Zone = station.ZoneName ?? "Sin zona";
        Ip = string.IsNullOrWhiteSpace(station.IpAddress) ? "—" : station.IpAddress!;
        Rate = station.RateName ?? "Sin tarifa";
        RatePerHour = station.RatePerHour;
        Status = Labels.StatusName(station.Status);
        StatusBrush = Labels.StatusColor(station.Status);
        Session = station.CurrentSessionId is int id ? $"#{id}" : "—";
        LastSeen = station.LastSeenUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
        CurrentUser = string.IsNullOrWhiteSpace(station.CurrentUser) ? "—" : station.CurrentUser!;
        Active = station.Active ? "Sí" : "No";
    }

    public StationDto Source { get; }
    public string Name { get; }
    public string Kind { get; }
    public string Platform { get; }
    public string Controllers { get; }
    public string Zone { get; }
    public string Ip { get; }
    public string Rate { get; }
    public decimal RatePerHour { get; }
    public string Status { get; }
    public string StatusBrush { get; }
    public string Session { get; }
    public string LastSeen { get; }
    public string CurrentUser { get; }
    public string Active { get; }
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
            .Select(r => new Option<int?>(r.Id, $"{r.Name} · $ {r.PricePerHour:N2} / hora")));
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
