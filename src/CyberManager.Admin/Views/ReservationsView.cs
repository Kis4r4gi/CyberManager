using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>Reservas de equipos por fecha y hora.</summary>
public sealed class ReservationsView : UserControl, IRefreshable
{
    private readonly DataGrid _grid;
    private readonly DatePicker _fromPicker = new() { SelectedDate = DateTime.Today };
    private readonly DatePicker _toPicker = new() { SelectedDate = DateTime.Today.AddDays(7) };
    private readonly TextBlock _pendingValue;
    private readonly TextBlock _confirmedValue;
    private readonly TextBlock _progressValue;
    private readonly TextBlock _cancelledValue;
    private readonly TextBlock _summaryText = Kit.Muted("Cargando reservas...");

    private List<ReservationDto> _reservations = new();
    private List<StationDto> _stations = new();
    private List<CustomerDto> _customers = new();

    public ReservationsView()
    {
        _grid = Kit.Table(
            Kit.Col("Cliente", nameof(ReservationRow.Customer), 200),
            Kit.Col("Teléfono", nameof(ReservationRow.Phone), 130),
            Kit.Col("Equipo", nameof(ReservationRow.Station), 140),
            Kit.Col("Inicio", nameof(ReservationRow.Start), 145),
            Kit.Col("Fin", nameof(ReservationRow.End), 145),
            Kit.Col("Duración", nameof(ReservationRow.Duration), 90),
            Kit.TemplateCol("Estado", Kit.Pill(nameof(ReservationRow.Status), nameof(ReservationRow.StatusBrush)), 130),
            Kit.Col("Notas", nameof(ReservationRow.Notes)));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(Kit.PageHeader("Reservas",
            "Aparte equipos para clientes en un rango de fecha y hora. Las reservas pendientes se avisan en el tablero.",
            Kit.Button("Nueva reserva", "PrimaryButton", NewReservation),
            Kit.Button("Actualizar", "GhostButton", () => _ = Ui.Run(RefreshAsync, "No se pudieron cargar las reservas"))));

        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
        metrics.Children.Add(Metric("Pendientes", "WarningBrush", out _pendingValue));
        metrics.Children.Add(Metric("Confirmadas", "Accent2Brush", out _confirmedValue));
        metrics.Children.Add(Metric("En curso", "AccentBrush", out _progressValue));
        metrics.Children.Add(Metric("Canceladas", "DangerBrush", out _cancelledValue));
        Grid.SetRow(metrics, 1);
        root.Children.Add(metrics);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var range = Kit.HStack(8, Kit.Label("Desde"), _fromPicker, Kit.Muted("hasta"), _toPicker);
        range.VerticalAlignment = VerticalAlignment.Center;
        toolbar.Children.Add(range);

        var actions = Kit.HStack(8,
            Kit.Button("Consultar", "BlueButton", () => Render()),
            Kit.Button("Editar", "GhostButton", EditReservation),
            Kit.Button("Confirmar", "GhostButton", ConfirmReservation),
            Kit.Button("Cancelar reserva", "DangerButton", CancelReservation));
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
        var hint = Kit.Muted("Doble clic en una reserva para editarla.");
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        footer.Children.Add(hint);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;

        _fromPicker.SelectedDateChanged += (_, _) => Render();
        _toPicker.SelectedDateChanged += (_, _) => Render();
        _grid.MouseDoubleClick += (_, _) => EditReservation();

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudieron cargar las reservas");
    }

    private static Border Metric(string label, string accent, out TextBlock valueText)
    {
        var card = Kit.StatCard(label, "—", null, accent, out var value);
        card.Margin = new Thickness(0, 0, 0, 0);
        valueText = value;
        return card;
    }

    private ReservationDto? Selected => (_grid.SelectedItem as ReservationRow)?.Source;

    public async Task RefreshAsync()
    {
        var reservationsTask = App.Api.ReservationsAsync();
        var stationsTask = App.Api.StationsAsync();
        var customersTask = App.Api.CustomersAsync();

        await Task.WhenAll(reservationsTask, stationsTask, customersTask);

        _reservations = await reservationsTask;
        _stations = await stationsTask;
        _customers = await customersTask;

        Render();
    }

    private void Render()
    {
        var from = (_fromPicker.SelectedDate ?? DateTime.Today).Date;
        var to = (_toPicker.SelectedDate ?? DateTime.Today.AddDays(7)).Date.AddDays(1);

        var filtered = _reservations
            .Where(r => r.EndUtc.ToLocalTime() >= from && r.StartUtc.ToLocalTime() < to)
            .OrderBy(r => r.StartUtc)
            .ToList();

        _grid.ItemsSource = filtered.Select(r => new ReservationRow(r)).ToList();

        _pendingValue.Text = filtered.Count(r => r.Status == ReservationStatus.Pending).ToString();
        _confirmedValue.Text = filtered.Count(r => r.Status == ReservationStatus.Confirmed).ToString();
        _progressValue.Text = filtered.Count(r => r.Status == ReservationStatus.InProgress).ToString();
        _cancelledValue.Text = filtered.Count(r => r.Status == ReservationStatus.Cancelled).ToString();

        var upcoming = filtered.Count(r => r.Status is ReservationStatus.Pending or ReservationStatus.Confirmed
                                           && r.StartUtc > DateTime.UtcNow);
        _summaryText.Text = $"{filtered.Count} reservas en el rango · {upcoming} próximas · " +
                            $"{from:dd/MM/yyyy} al {to.AddDays(-1):dd/MM/yyyy}";
    }

    private void NewReservation()
    {
        if (_stations.Count == 0)
        {
            Ui.Info("Registre al menos un equipo antes de crear reservas.", "Reservas");
            return;
        }

        var dialog = new ReservationDialog(_stations, _customers) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveReservationAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo crear la reserva");
    }

    private void EditReservation()
    {
        if (Selected is not { } reservation)
        {
            Ui.Info("Seleccione una reserva de la lista.", "Reservas");
            return;
        }

        var dialog = new ReservationDialog(_stations, _customers, reservation) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveReservationAsync(reservation.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar la reserva");
    }

    private void ConfirmReservation()
    {
        if (Selected is not { } reservation)
        {
            Ui.Info("Seleccione una reserva de la lista.", "Reservas");
            return;
        }

        if (reservation.Status == ReservationStatus.Confirmed) return;

        _ = Ui.Run(async () =>
        {
            var request = new SaveReservationRequest(reservation.StationId, reservation.CustomerId,
                reservation.CustomerName, reservation.Phone, reservation.StartUtc, reservation.EndUtc,
                ReservationStatus.Confirmed, reservation.Notes);
            await App.Api.SaveReservationAsync(reservation.Id, request);
            await RefreshAsync();
        }, "No se pudo confirmar la reserva");
    }

    private void CancelReservation()
    {
        if (Selected is not { } reservation)
        {
            Ui.Info("Seleccione una reserva de la lista.", "Reservas");
            return;
        }

        if (!Ui.Confirm($"¿Cancelar la reserva de {reservation.CustomerName} para {reservation.StationName}?",
                "Cancelar reserva"))
            return;

        _ = Ui.Run(async () =>
        {
            await App.Api.CancelReservationAsync(reservation.Id);
            await RefreshAsync();
        }, "No se pudo cancelar la reserva");
    }
}

public sealed class ReservationRow
{
    public ReservationRow(ReservationDto reservation)
    {
        Source = reservation;
        Customer = reservation.CustomerName;
        Phone = string.IsNullOrWhiteSpace(reservation.Phone) ? "—" : reservation.Phone!;
        Station = reservation.StationName;
        Start = reservation.StartUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        End = reservation.EndUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        var minutes = Math.Max(0, (int)(reservation.EndUtc - reservation.StartUtc).TotalMinutes);
        Duration = $"{minutes} min";
        Status = Labels.ReservationName(reservation.Status);
        StatusBrush = Labels.ReservationColor(reservation.Status);
        Notes = string.IsNullOrWhiteSpace(reservation.Notes) ? "—" : reservation.Notes!;
    }

    public ReservationDto Source { get; }
    public string Customer { get; }
    public string Phone { get; }
    public string Station { get; }
    public string Start { get; }
    public string End { get; }
    public string Duration { get; }
    public string Status { get; }
    public string StatusBrush { get; }
    public string Notes { get; }
}

public sealed class ReservationDialog : FormDialog
{
    private readonly ComboBox _stationBox;
    private readonly ComboBox _customerBox;
    private readonly TextBox _name;
    private readonly TextBox _phone;
    private readonly DatePicker _startDate;
    private readonly TextBox _startTime;
    private readonly DatePicker _endDate;
    private readonly TextBox _endTime;
    private readonly ComboBox _statusBox;
    private readonly TextBox _notes;

    public SaveReservationRequest? Result { get; private set; }

    public ReservationDialog(List<StationDto> stations, List<CustomerDto> customers, ReservationDto? reservation = null)
        : base(reservation is null ? "Nueva reserva" : "Editar reserva",
            "Indique el equipo, el horario y los datos de contacto del cliente.", width: 520)
    {
        var stationOptions = stations
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new Option<StationDto>(s, $"{s.Name} · {s.ZoneName ?? "sin zona"}"))
            .ToList();
        _stationBox = AddCombo("Equipo reservado", stationOptions);
        if (reservation is not null)
        {
            var option = stationOptions.FirstOrDefault(o => o.Value.Id == reservation.StationId);
            if (option is not null) _stationBox.SelectedItem = option;
        }

        var customerOptions = new List<Option<int?>> { new(null, "Cliente ocasional / sin registrar") };
        customerOptions.AddRange(customers.OrderBy(c => c.Name)
            .Select(c => new Option<int?>(c.Id, c.Name)));
        _customerBox = AddCombo("Cliente registrado (opcional)", customerOptions);
        if (reservation?.CustomerId is int customerId)
        {
            var option = customerOptions.FirstOrDefault(o => o.Value == customerId);
            if (option is not null) _customerBox.SelectedItem = option;
        }

        _name = Add(new TextBox { Text = reservation?.CustomerName ?? string.Empty }, "Nombre del cliente");
        _phone = Add(new TextBox { Text = reservation?.Phone ?? string.Empty }, "Teléfono de contacto");

        _customerBox.SelectionChanged += (_, _) =>
        {
            if (_customerBox.SelectedItem is not Option<int?> { Value: int id }) return;
            var customer = customers.FirstOrDefault(c => c.Id == id);
            if (customer is null) return;

            if (string.IsNullOrWhiteSpace(_name.Text)) _name.Text = customer.Name;
            if (string.IsNullOrWhiteSpace(_phone.Text)) _phone.Text = customer.Phone ?? string.Empty;
        };

        AddSeparator();
        Body.Children.Add(new TextBlock
        {
            Text = "HORARIO",
            Style = Style("Label"),
            Margin = new Thickness(0, 2, 0, 6)
        });

        var localStart = reservation?.StartUtc.ToLocalTime() ?? RoundUp(DateTime.Now);
        var localEnd = reservation?.EndUtc.ToLocalTime() ?? localStart.AddHours(1);

        var startRow = new Grid();
        startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        _startDate = new DatePicker { SelectedDate = localStart.Date };
        startRow.Children.Add(_startDate);

        _startTime = new TextBox { Text = localStart.ToString("HH:mm"), Margin = new Thickness(0) };
        Grid.SetColumn(_startTime, 2);
        startRow.Children.Add(_startTime);
        Body.Children.Add(new TextBlock { Text = "Inicio (fecha y hora)", Style = Style("Label"), Margin = new Thickness(0, 8, 0, 4) });
        Body.Children.Add(startRow);

        var endRow = new Grid();
        endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        _endDate = new DatePicker { SelectedDate = localEnd.Date };
        endRow.Children.Add(_endDate);

        _endTime = new TextBox { Text = localEnd.ToString("HH:mm"), Margin = new Thickness(0) };
        Grid.SetColumn(_endTime, 2);
        endRow.Children.Add(_endTime);
        Body.Children.Add(new TextBlock { Text = "Fin (fecha y hora)", Style = Style("Label"), Margin = new Thickness(0, 8, 0, 4) });
        Body.Children.Add(endRow);

        _statusBox = AddCombo("Estado de la reserva", new[]
        {
            new Option<ReservationStatus>(ReservationStatus.Pending, "Pendiente"),
            new Option<ReservationStatus>(ReservationStatus.Confirmed, "Confirmada"),
            new Option<ReservationStatus>(ReservationStatus.InProgress, "En curso"),
            new Option<ReservationStatus>(ReservationStatus.Completed, "Completada"),
            new Option<ReservationStatus>(ReservationStatus.NoShow, "No asistió"),
            new Option<ReservationStatus>(ReservationStatus.Cancelled, "Cancelada")
        });
        if (reservation is not null)
        {
            var index = ((List<Option<ReservationStatus>>)_statusBox.ItemsSource)
                .FindIndex(o => o.Value == reservation.Status);
            if (index >= 0) _statusBox.SelectedIndex = index;
        }

        _notes = new TextBox { Text = reservation?.Notes ?? string.Empty, AcceptsReturn = true, Height = 60, TextWrapping = TextWrapping.Wrap };
        _notes.Margin = new Thickness(0);
        Body.Children.Add(new TextBlock { Text = "Notas", Style = Style("Label"), Margin = new Thickness(0, 10, 0, 4) });
        Body.Children.Add(_notes);
    }

    private static DateTime RoundUp(DateTime value)
    {
        var minute = ((value.Minute / 15) + 1) * 15;
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0).AddMinutes(minute);
    }

    private static DateTime Combine(DatePicker picker, TextBox time, string field)
    {
        if (picker.SelectedDate is not DateTime date)
            throw new InvalidOperationException($"Seleccione la fecha de {field}.");

        if (!TimeSpan.TryParseExact(time.Text.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var span)
            && !TimeSpan.TryParseExact(time.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out span))
            throw new InvalidOperationException($"La hora de {field} debe tener el formato HH:mm.");

        if (span < TimeSpan.Zero || span >= TimeSpan.FromDays(1))
            throw new InvalidOperationException($"La hora de {field} no es válida.");

        return date.Date + span;
    }

    protected override void OnAccept()
    {
        if (_stationBox.SelectedItem is not Option<StationDto> station)
            throw new InvalidOperationException("Seleccione el equipo a reservar.");

        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre del cliente.");

        var startLocal = Combine(_startDate, _startTime, "inicio");
        var endLocal = Combine(_endDate, _endTime, "fin");

        if (endLocal <= startLocal)
            throw new InvalidOperationException("La hora final debe ser mayor a la hora inicial.");

        var status = (_statusBox.SelectedItem as Option<ReservationStatus>)?.Value ?? ReservationStatus.Pending;
        var customerId = (_customerBox.SelectedItem as Option<int?>)?.Value;

        Result = new SaveReservationRequest(
            station.Value.Id,
            customerId,
            _name.Text.Trim(),
            string.IsNullOrWhiteSpace(_phone.Text) ? null : _phone.Text.Trim(),
            DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime(),
            DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime(),
            status,
            string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim());

        base.OnAccept();
    }
}
