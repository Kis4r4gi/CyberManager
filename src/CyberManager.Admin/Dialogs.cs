using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin;

public sealed record Option<T>(T Value, string Text)
{
    /// <summary>Texto visible traducido al idioma actual.</summary>
    public string DisplayText => L.T(Text);
}

/// <summary>Ventana modal oscura con formulario generado por código.</summary>
public class FormDialog : Window
{
    public StackPanel Body { get; } = new();
    protected Button AcceptButton { get; }
    protected Button CancelButton { get; }
    private readonly TextBlock _error;

    protected FormDialog(string title, string? subtitle = null, string acceptText = "Aceptar", double width = 480)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = Brush("BgBrush");

        var card = new Border
        {
            Style = Style("Card"),
            Margin = new Thickness(18)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, Style = Style("H2") });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Style = Style("Muted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 8)
            });
        }

        stack.Children.Add(new ScrollViewer
        {
            Content = Body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 430
        });

        _error = new TextBlock
        {
            Foreground = Brush("DangerBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed
        };
        stack.Children.Add(_error);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };

        CancelButton = new Button
        {
            Content = "Cancelar",
            Style = Style("GhostButton"),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
            ToolTip = L.T("Cierra sin guardar cambios.")
        };
        CancelButton.Click += (_, _) => { DialogResult = false; Close(); };

        AcceptButton = new Button
        {
            Content = acceptText,
            Style = Style("PrimaryButton"),
            ToolTip = L.T("Guarda los datos del formulario.")
        };
        ToolTipService.SetInitialShowDelay(AcceptButton, 2000);
        ToolTipService.SetInitialShowDelay(CancelButton, 2000);
        AcceptButton.Click += (_, _) =>
        {
            try
            {
                OnAccept();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        };

        buttons.Children.Add(CancelButton);
        buttons.Children.Add(AcceptButton);
        stack.Children.Add(buttons);

        card.Child = stack;
        Content = card;
    }

    protected void HideAccept() => AcceptButton.Visibility = Visibility.Collapsed;
    protected void HideCancel() => CancelButton.Visibility = Visibility.Collapsed;

    protected static new Style Style(string key) => (Style)Application.Current.FindResource(key);
    protected static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);

    protected T Add<T>(T control, string? label = null) where T : FrameworkElement
    {
        if (label is not null)
        {
            Body.Children.Add(new TextBlock
            {
                Text = label,
                Style = Style("Label"),
                Margin = new Thickness(0, 10, 0, 4)
            });
        }

        control.Margin = new Thickness(0, 0, 0, 2);
        Body.Children.Add(control);
        return control;
    }

    protected ComboBox AddCombo<T>(string label, IEnumerable<Option<T>> items)
    {
        var list = items.ToList();
        var combo = new ComboBox
        {
            ItemsSource = list,
            DisplayMemberPath = nameof(Option<object>.DisplayText),
            SelectedIndex = list.Count > 0 ? 0 : -1
        };
        return Add(combo, label);
    }

    protected void AddRow(string label, string value, bool highlight = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.Children.Add(new TextBlock { Text = label, Style = Style("Muted"), VerticalAlignment = VerticalAlignment.Center });
        grid.Children.Add(new TextBlock
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = highlight ? FontWeights.Bold : FontWeights.SemiBold,
            FontSize = highlight ? 16 : 13,
            Foreground = highlight ? Brush("AccentBrush") : Brush("TextBrush")
        });
        Body.Children.Add(grid);
    }

    protected void AddSeparator() => Body.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

    protected void AddNote(string text, string brushKey = "MutedBrush")
    {
        Body.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Brush(brushKey),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Margin = new Thickness(0, 10, 0, 0)
        });
    }

    protected CheckBox AddCheck(string text, bool initial)
    {
        var check = new CheckBox
        {
            Content = text,
            IsChecked = initial,
            Margin = new Thickness(0, 12, 0, 2)
        };
        Body.Children.Add(check);
        return check;
    }

    protected void ShowError(string message)
    {
        _error.Text = message;
        _error.Visibility = Visibility.Visible;
    }

    protected virtual void OnAccept() => DialogResult = true;

    protected static decimal ParseDecimal(TextBox box, string field, decimal minimum = 0m)
    {
        if (!decimal.TryParse(box.Text.Trim(), out var value))
            throw new InvalidOperationException($"El campo '{field}' debe ser un número.");
        if (value < minimum)
            throw new InvalidOperationException($"El campo '{field}' debe ser mayor o igual a {minimum:N2}.");
        return value;
    }

    protected static int ParseInt(TextBox box, string field, int minimum = 0)
    {
        if (!int.TryParse(box.Text.Trim(), out var value))
            throw new InvalidOperationException($"El campo '{field}' debe ser un número entero.");
        if (value < minimum)
            throw new InvalidOperationException($"El campo '{field}' debe ser mayor o igual a {minimum}.");
        return value;
    }
}

public sealed class StartSessionDialog : FormDialog
{
    private readonly StationDto _station;
    private readonly ComboBox _typeBox;
    private readonly ComboBox _rateBox;
    private readonly TextBox _minutesBox;
    private readonly ComboBox _customerBox;
    private readonly CheckBox _chargeBox;
    private readonly ComboBox _methodBox;
    private readonly ComboBox? _packageBox;
    private readonly TextBlock? _packageInfo;

    public StartSessionRequest? Result { get; private set; }

    private TimePackageDto? SelectedPackage =>
        (_packageBox?.SelectedItem as Option<TimePackageDto?>)?.Value;

    public StartSessionDialog(StationDto station, List<RateDto> rates, List<CustomerDto> customers,
        List<TimePackageDto>? packages = null)
        : base($"Iniciar sesión · {station.Name}", "Configure la modalidad y el tiempo inicial del equipo.")
    {
        _station = station;

        _typeBox = AddCombo("Modalidad", new[]
        {
            new Option<SessionType>(SessionType.Prepaid, "Prepago (paga por adelantado)"),
            new Option<SessionType>(SessionType.Postpaid, "Postpago (paga al salir)"),
            new Option<SessionType>(SessionType.Free, "Cortesía (sin cobro)")
        });

        _rateBox = AddCombo("Tarifa por hora", rates.Select(r =>
            new Option<RateDto>(r, $"{r.Name} · $ {r.PricePerHour:N2} / hora")));
        var stationRate = rates.FirstOrDefault(r => r.Id == station.RateId);
        if (stationRate is not null)
        {
            var option = _rateBox.Items.Cast<Option<RateDto>>().FirstOrDefault(o => o.Value.Id == stationRate.Id);
            if (option is not null) _rateBox.SelectedItem = option;
        }

        _minutesBox = Add(new TextBox { Text = "60" }, "Minutos (solo prepago)");

        // Paquetes de tiempo configurados en Productos y tarifas. Al elegir uno se
        // cargan los minutos con bono y su precio se cobra por adelantado.
        var packageOptions = new List<Option<TimePackageDto?>>
        {
            new(null, "Sin paquete (minutos manuales)")
        };
        packageOptions.AddRange((packages ?? new List<TimePackageDto>())
            .Where(p => p.Active)
            .Select(p => new Option<TimePackageDto?>(p,
                $"{p.Name} · {p.Minutes + p.BonusMinutes} min · $ {p.Price:N2}")));

        _packageBox = AddCombo("Paquete de tiempo", packageOptions);
        _packageInfo = new TextBlock
        {
            Style = Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Body.Children.Add(_packageInfo);
        _packageBox.SelectionChanged += (_, _) => ApplyPackage();

        var customerOptions = new List<Option<int?>> { new(null, "Sin cliente registrado") };
        customerOptions.AddRange(customers.Select(c =>
            new Option<int?>(c.Id, $"{c.Name} · saldo $ {c.Balance:N2}")));
        _customerBox = AddCombo("Cliente", customerOptions);

        _chargeBox = AddCheck("Cobrar el tiempo ahora (requiere caja abierta)", true);

        _methodBox = AddCombo("Forma de pago del tiempo", new[]
        {
            new Option<PaymentMethod>(PaymentMethod.Cash, "Efectivo en bolívares"),
            new Option<PaymentMethod>(PaymentMethod.CashUsd, "Efectivo en dólares (IGTF)"),
            new Option<PaymentMethod>(PaymentMethod.Card, "Punto de venta / tarjeta"),
            new Option<PaymentMethod>(PaymentMethod.Transfer, "Transferencia"),
            new Option<PaymentMethod>(PaymentMethod.PagoMovil, "Pago Móvil")
        });

        AddNote("Los consumos que pida el cliente desde su pantalla se cargan a la sesión y se cobran al cerrarla.");
        ApplyPackage();
    }

    private void ApplyPackage()
    {
        if (_packageBox is null || _packageInfo is null) return;

        if (SelectedPackage is not { } package)
        {
            _packageInfo.Text = string.Empty;
            return;
        }

        _typeBox.SelectedIndex = 0; // prepago
        _minutesBox.Text = (package.Minutes + package.BonusMinutes).ToString();
        _chargeBox.IsChecked = true;
        _packageInfo.Text = package.BonusMinutes > 0
            ? $"Incluye {package.BonusMinutes} min de bono. Se cobran $ {package.Price:N2} por adelantado."
            : $"Se cobran $ {package.Price:N2} por adelantado.";
    }

    protected override void OnAccept()
    {
        var type = ((Option<SessionType>)_typeBox.SelectedItem!).Value;
        var rate = ((Option<RateDto>)_rateBox.SelectedItem!).Value;
        var customerId = ((Option<int?>)_customerBox.SelectedItem!).Value;
        var method = ((Option<PaymentMethod>)_methodBox.SelectedItem!).Value;

        var minutes = type == SessionType.Prepaid ? ParseInt(_minutesBox, "Minutos", 1) : 0;
        var charge = _chargeBox.IsChecked == true && type == SessionType.Prepaid;

        var package = type == SessionType.Prepaid ? SelectedPackage : null;
        decimal? upfront = null;
        if (package is not null)
        {
            minutes = Math.Max(1, package.Minutes + package.BonusMinutes);
            upfront = package.Price;
            charge = true;
        }

        Result = new StartSessionRequest(
            _station.Id, type, minutes, customerId, rate.Id, null, null, charge, upfront, method);

        base.OnAccept();
    }
}

/// <summary>Control de volumen maestro de un equipo remoto.</summary>
public sealed class VolumeDialog : FormDialog
{
    private readonly Slider _slider;
    private readonly TextBlock _value;

    public int Volume { get; private set; }

    public VolumeDialog(string stationName, int initial = 50)
        : base("Volumen del equipo", $"Ajuste el volumen maestro de {stationName}.")
    {
        var clamped = Math.Clamp(initial, 0, 100);
        Volume = clamped;

        _value = new TextBlock
        {
            Text = $"{clamped} %",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("AccentBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        };

        _slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = clamped,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            Height = 36
        };
        _slider.ValueChanged += (_, _) =>
        {
            Volume = (int)Math.Round(_slider.Value);
            _value.Text = $"{Volume} %";
        };

        var panel = new StackPanel();
        panel.Children.Add(_value);
        panel.Children.Add(_slider);
        Add(panel, "Volumen (%)");
        AddNote("El cambio se aplica de inmediato en el equipo seleccionado.");
    }

    protected override void OnAccept()
    {
        Volume = (int)Math.Round(_slider.Value);
        base.OnAccept();
    }
}

public sealed class StopSessionDialog : FormDialog
{
    private readonly SessionDto _session;
    private readonly ComboBox _methodBox;
    private readonly CheckBox _chargeBox;

    public StopSessionRequest? Result { get; private set; }

    public StopSessionDialog(SessionDto session, decimal exchangeRate, bool cashOpen)
        : base($"Cobrar y cerrar · {session.StationName}", "Revise el resumen antes de finalizar la sesión.")
    {
        _session = session;

        AddRow("Tiempo transcurrido", $"{session.ElapsedMinutes} min");
        AddRow("Cargo por tiempo", $"$ {session.TimeAmount:N2}");
        AddRow("Consumos", $"$ {session.ConsumptionAmount:N2}");
        AddSeparator();
        AddRow("Total USD", $"$ {session.TotalAmount:N2}", highlight: true);
        AddRow("Total Bs", $"Bs {session.TotalAmount * exchangeRate:N2}");
        AddSeparator();

        var methods = new List<Option<PaymentMethod>>
        {
            new(PaymentMethod.Cash, "Efectivo en bolívares"),
            new(PaymentMethod.CashUsd, "Efectivo en dólares (IGTF)"),
            new(PaymentMethod.Card, "Punto de venta / tarjeta"),
            new(PaymentMethod.Transfer, "Transferencia"),
            new(PaymentMethod.PagoMovil, "Pago Móvil")
        };
        if (session.CustomerId is not null)
            methods.Add(new Option<PaymentMethod>(PaymentMethod.CustomerBalance, "Cargar al saldo del cliente"));

        _methodBox = AddCombo("Forma de pago", methods);

        _chargeBox = AddCheck("Cobrar el tiempo al cerrar", session.Type != SessionType.Prepaid || session.Overtime);

        if (session.Type == SessionType.Prepaid && !session.Overtime)
            AddNote("El tiempo prepago ya fue cobrado al iniciar. Solo se cobrarán los consumos pendientes.");

        if (!cashOpen)
        {
            AddNote("No hay una caja abierta. Abra la caja antes de cobrar la sesión.", "DangerBrush");
            AcceptButton.IsEnabled = false;
        }
    }

    protected override void OnAccept()
    {
        var method = ((Option<PaymentMethod>)_methodBox.SelectedItem!).Value;
        var charge = _chargeBox.IsChecked == true;

        if (method == PaymentMethod.CustomerBalance && _session.CustomerId is null)
            throw new InvalidOperationException("La sesión no tiene un cliente asociado.");

        Result = new StopSessionRequest(_session.Id, method, charge, null);
        base.OnAccept();
    }
}

public sealed class ExtendSessionDialog : FormDialog
{
    private readonly SessionDto _session;
    private readonly TextBox _minutesBox;
    private readonly CheckBox _chargeBox;
    private readonly ComboBox _methodBox;

    public ExtendSessionRequest? Result { get; private set; }

    public ExtendSessionDialog(SessionDto session)
        : base($"Extender sesión · {session.StationName}", $"Tiempo actual: {session.ElapsedMinutes} min transcurridos.")
    {
        _session = session;

        _minutesBox = Add(new TextBox { Text = "30" }, "Minutos adicionales");
        _chargeBox = AddCheck("Cobrar la extensión ahora (requiere caja abierta)", true);
        _methodBox = AddCombo("Forma de pago", new[]
        {
            new Option<PaymentMethod>(PaymentMethod.Cash, "Efectivo en bolívares"),
            new Option<PaymentMethod>(PaymentMethod.CashUsd, "Efectivo en dólares (IGTF)"),
            new Option<PaymentMethod>(PaymentMethod.Card, "Punto de venta / tarjeta"),
            new Option<PaymentMethod>(PaymentMethod.Transfer, "Transferencia"),
            new Option<PaymentMethod>(PaymentMethod.PagoMovil, "Pago Móvil")
        });

        AddNote($"Valor estimado: $ {session.RatePerHour * 30 / 60m:N2} por 30 minutos a $ {session.RatePerHour:N2}/h.");
    }

    protected override void OnAccept()
    {
        var minutes = ParseInt(_minutesBox, "Minutos adicionales", 1);
        var method = ((Option<PaymentMethod>)_methodBox.SelectedItem!).Value;

        Result = new ExtendSessionRequest(_session.Id, minutes, _chargeBox.IsChecked == true, method);
        base.OnAccept();
    }
}

public sealed class TransferSessionDialog : FormDialog
{
    private readonly ComboBox _targetBox;

    public int? TargetStationId { get; private set; }

    public TransferSessionDialog(string currentStation, List<StationDto> targets)
        : base("Transferir sesión", $"Mover la sesión de {currentStation} a otro equipo disponible.")
    {
        _targetBox = AddCombo("Equipo destino", targets.Select(s =>
            new Option<StationDto>(s, $"{s.Name} · {s.ZoneName ?? "sin zona"}")));

        if (targets.Count == 0)
        {
            AddNote("No hay equipos disponibles para transferir la sesión.", "DangerBrush");
            AcceptButton.IsEnabled = false;
        }
    }

    protected override void OnAccept()
    {
        TargetStationId = ((Option<StationDto>)_targetBox.SelectedItem!).Value.Id;
        base.OnAccept();
    }
}

public sealed class PromptDialog : FormDialog
{
    private readonly TextBox _box;

    public string? Result { get; private set; }

    public PromptDialog(string title, string label, string initial = "", string? subtitle = null, bool multiline = false)
        : base(title, subtitle)
    {
        _box = Add(new TextBox
        {
            Text = initial,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Height = multiline ? 90 : double.NaN,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
        }, label);
    }

    protected override void OnAccept()
    {
        Result = _box.Text.Trim();
        base.OnAccept();
    }
}

