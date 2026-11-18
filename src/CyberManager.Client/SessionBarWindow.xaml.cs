using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

public partial class SessionBarWindow : Window
{
    private readonly DispatcherTimer _ticker =
        new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
    private StationClientStateDto? _state;
    private DateTime _anchorAt = DateTime.UtcNow;
    private int _anchorSeconds;
    private bool _hasAnchor;
    private bool _paused;
    private bool _prepaid;
    private bool _countdown;
    private bool _overtime;
    private int _sessionId;
    private string? _warningText;
    private bool _collapsed;

    public event Action? OrderRequested;
    /// <summary>El cliente prepago quiere comprar más minutos.</summary>
    public event Action? RechargeRequested;
    /// <summary>El cliente prepago quiere cerrar su sesión en este equipo.</summary>
    public event Action? FinishRequested;

    public SessionBarWindow()
    {
        InitializeComponent();

        _ticker.Tick += (_, _) => Tick();
        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            ApplyCollapsedState();
            PositionTopCenter();
            _ticker.Start();
        };
        SizeChanged += (_, _) => PositionTopCenter();
        Closed += (_, _) => _ticker.Stop();
    }

    public void UpdateState(StationClientStateDto state)
    {
        _state = state;

        StationText.Text = state.Name;

        // La descripción es opcional: si no hay texto configurado no se muestra
        // ningún renglón extra en la barra.
        var description = state.SessionBarDescription?.Trim();
        DescriptionText.Text = description ?? string.Empty;
        DescriptionText.Visibility = string.IsNullOrEmpty(description)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var scale = Math.Clamp(state.SessionBarScale, 70, 160) / 100d;
        RootScale.ScaleX = scale;
        RootScale.ScaleY = scale;

        var session = state.Session;
        var customer = session?.CustomerName?.Trim();
        var hasCustomer = !string.IsNullOrEmpty(customer);
        CustomerText.Text = customer ?? string.Empty;
        CustomerText.Visibility = hasCustomer ? Visibility.Visible : Visibility.Collapsed;
        CustomerSeparator.Visibility = hasCustomer ? Visibility.Visible : Visibility.Collapsed;

        if (session is null)
        {
            TimeLabel.Text = L.T("SESIÓN");
            TimeText.Text = "—";
            AmountText.Text = "$ 0,00";
            AmountBsText.Text = "Bs 0,00";
            RechargeButton.Visibility = Visibility.Collapsed;
            FinishButton.Visibility = Visibility.Collapsed;
            _hasAnchor = false;
            return;
        }

        if (_sessionId != session.Id)
        {
            _sessionId = session.Id;
            _warningText = null;
            _hasAnchor = false;
        }

        if (!session.Overtime && session.RemainingMinutes > 5)
            _warningText = null;

        _prepaid = session.Type == SessionType.Prepaid;
        // En prepago el reloj muestra el tiempo restante, así que avanza hacia
        // atrás; en postpago muestra el tiempo transcurrido y avanza hacia adelante.
        _countdown = _prepaid;
        _overtime = session.Overtime;
        RechargeButton.Visibility = _prepaid ? Visibility.Visible : Visibility.Collapsed;
        FinishButton.Visibility = _prepaid ? Visibility.Visible : Visibility.Collapsed;

        var paused = session.Status == SessionStatus.Paused;
        var serverSeconds = _prepaid
            ? (session.Overtime ? session.RemainingMinutes : Math.Max(session.RemainingMinutes, 0)) * 60
            : session.ElapsedMinutes * 60;

        // El servidor entrega minutos enteros; si se reajustara el ancla en cada
        // aviso el contador retrocedería hasta 59 s. Solo se reajusta al cambiar
        // de sesión, al pausar/reanudar o si el reloj local se desvía demasiado.
        if (!_hasAnchor || paused != _paused || Math.Abs(CurrentSeconds() - serverSeconds) > 90)
        {
            _anchorAt = DateTime.UtcNow;
            _anchorSeconds = serverSeconds;
            _hasAnchor = true;
        }
        _paused = paused;

        TimeLabel.Text = _prepaid ? L.T("TIEMPO RESTANTE") : L.T("TIEMPO TRANSCURRIDO");

        var symbol = string.IsNullOrWhiteSpace(state.LocalCurrencySymbol)
            ? "Bs"
            : state.LocalCurrencySymbol;

        AmountText.Text = $"$ {session.TotalAmount:N2}";
        AmountBsText.Text = $"{symbol} {session.TotalAmount * state.ExchangeRate:N2}";

        AccountTypeText.Text = session.Type switch
        {
            SessionType.Prepaid => L.IsEnglish
                ? $"Prepaid · {session.PlannedMinutes} min"
                : $"Prepago · {session.PlannedMinutes} min",
            SessionType.Postpaid => L.T("Postpago"),
            _ => L.T("Cortesía")
        };
        AccountStartedText.Text = session.StartedUtc.ToLocalTime().ToString("HH:mm");
        AccountTimeUsdText.Text = $"$ {session.TimeAmount:N2}";
        AccountTimeBsText.Text = $"{symbol} {session.TimeAmount * state.ExchangeRate:N2}";
        AccountConsumptionUsdText.Text = $"$ {session.ConsumptionAmount:N2}";
        AccountConsumptionBsText.Text = $"{symbol} {session.ConsumptionAmount * state.ExchangeRate:N2}";
        AccountTotalUsdText.Text = $"$ {session.TotalAmount:N2}";
        AccountTotalBsText.Text = $"{symbol} {session.TotalAmount * state.ExchangeRate:N2}";
        AccountRateText.Text = $"1 USD = {state.ExchangeRate:N2} {symbol}";

        if (!string.IsNullOrWhiteSpace(_warningText))
        {
            WarningText.Text = _warningText;
            WarningText.Visibility = Visibility.Visible;
        }
        else
        {
            WarningText.Visibility = Visibility.Collapsed;
        }

        Tick();
    }

    public void ShowWarning(SessionWarningEvent warning)
    {
        _warningText = warning.Overtime
            ? L.IsEnglish ? "Time is up! Please go to the cashier." : "¡Tiempo agotado! Pase por caja."
            : L.IsEnglish
                ? $"{Math.Max(warning.RemainingMinutes, 0)} min remaining."
                : $"Quedan {Math.Max(warning.RemainingMinutes, 0)} min.";

        WarningText.Text = _warningText;
        WarningText.Visibility = Visibility.Visible;

        if (App.State is not null) UpdateState(App.State);
    }

    /// <summary>Indica que el tiempo mostrado proviene del respaldo local del cliente.</summary>
    public void ShowRestoredBackup()
    {
        _warningText = L.IsEnglish
            ? "No connection: time restored from the local backup."
            : "Sin conexión: tiempo restaurado desde el respaldo local.";
        WarningText.Text = _warningText;
        WarningText.Visibility = Visibility.Visible;
    }

    private int CurrentSeconds()
    {
        if (!_hasAnchor) return 0;
        if (_paused) return _anchorSeconds;

        var delta = (int)Math.Max(0, (DateTime.UtcNow - _anchorAt).TotalSeconds);
        return _countdown ? _anchorSeconds - delta : _anchorSeconds + delta;
    }

    private void Tick()
    {
        var session = _state?.Session;
        if (session is null || !_hasAnchor) return;

        var seconds = CurrentSeconds();
        var accent = (Brush)FindResource("AccentBrush");
        var danger = (Brush)FindResource("DangerBrush");
        var warning = (Brush)FindResource("WarningBrush");

        string text;
        Brush color;
        string? warningText = null;

        if (_prepaid && session.Type != SessionType.Free)
        {
            if (seconds >= 0)
            {
                text = Format(seconds);
                color = seconds <= 300 ? warning : accent;
                warningText = _warningText;
            }
            else
            {
                text = "+" + Format(-seconds);
                color = danger;
                warningText = L.T("¡Tiempo agotado!");
            }
        }
        else
        {
            text = Format(seconds);
            color = accent;
            if (_overtime)
            {
                color = danger;
                warningText = L.T("¡Tiempo agotado!");
            }
        }

        TimeText.Text = text;
        TimeText.Foreground = color;
        CollapsedTimeText.Text = text;
        CollapsedTimeText.Foreground = color;
        CollapsedWarningText.Visibility = warningText is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (warningText is null)
        {
            WarningText.Visibility = Visibility.Collapsed;
        }
        else
        {
            WarningText.Text = warningText;
            WarningText.Visibility = Visibility.Visible;
        }
    }

    private static string Format(int seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Abs(seconds));
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";
    }

    private void PositionTopCenter()
    {
        if (ActualWidth <= 0) return;
        // Se usa el área de trabajo para no quedar debajo de la barra de tareas.
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Left + (area.Width - ActualWidth) / 2);
        Top = area.Top + 8;
    }

    private void Order_Click(object sender, RoutedEventArgs e) => OrderRequested?.Invoke();

    private void Recharge_Click(object sender, RoutedEventArgs e) => RechargeRequested?.Invoke();

    private void Finish_Click(object sender, RoutedEventArgs e) => FinishRequested?.Invoke();

    private void Account_Click(object sender, RoutedEventArgs e)
    {
        AccountPopup.IsOpen = !AccountPopup.IsOpen;
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => SetCollapsed(true);

    private void Expand_Click(object sender, MouseButtonEventArgs e) => SetCollapsed(false);

    /// <summary>
    /// Oculta la barra y deja un icono flotante con el tiempo de la sesión. El
    /// estado se conserva mientras el cliente siga abierto, incluso si la barra
    /// se oculta entre sesiones.
    /// </summary>
    private void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
        {
            ApplyCollapsedState();
            return;
        }

        _collapsed = collapsed;
        if (collapsed) AccountPopup.IsOpen = false;

        ApplyCollapsedState();
        UpdateLayout();
        PositionTopCenter();
        Tick();
    }

    private void ApplyCollapsedState()
    {
        ExpandedPanel.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapsedPanel.Visibility = _collapsed ? Visibility.Visible : Visibility.Collapsed;
    }
}

