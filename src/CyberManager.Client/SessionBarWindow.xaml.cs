using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

public partial class SessionBarWindow : Window
{
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromSeconds(1) };
    private StationClientStateDto? _state;
    private DateTime _baseAt = DateTime.UtcNow;
    private int _baseSeconds;
    private bool _prepaid;
    private bool _overtime;
    private int _sessionId;
    private string? _warningText;

    public event Action? OrderRequested;

    public SessionBarWindow()
    {
        InitializeComponent();

        _ticker.Tick += (_, _) => Tick();
        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            PositionTopCenter();
            _ticker.Start();
        };
        SizeChanged += (_, _) => PositionTopCenter();
        Closed += (_, _) => _ticker.Stop();
    }

    public void UpdateState(StationClientStateDto state)
    {
        _state = state;
        _baseAt = DateTime.UtcNow;

        StationText.Text = state.Name;
        DescriptionText.Text = string.IsNullOrWhiteSpace(state.SessionBarDescription)
            ? L.T("Sesión activa")
            : state.SessionBarDescription!;
        var scale = Math.Clamp(state.SessionBarScale, 70, 160) / 100d;
        RootScale.ScaleX = scale;
        RootScale.ScaleY = scale;

        var session = state.Session;
        CustomerText.Text = session?.CustomerName ?? L.T("Sesión activa");

        if (session is null)
        {
            TimeLabel.Text = L.T("SESIÓN");
            TimeText.Text = "—";
            AmountText.Text = "$ 0,00";
            AmountBsText.Text = "Bs 0,00";
            return;
        }

        if (_sessionId != session.Id)
        {
            _sessionId = session.Id;
            _warningText = null;
        }

        if (!session.Overtime && session.RemainingMinutes > 5)
            _warningText = null;

        _prepaid = session.Type == SessionType.Prepaid;
        _overtime = session.Overtime;
        _baseSeconds = _prepaid ? Math.Max(session.RemainingMinutes, 0) * 60 : session.ElapsedMinutes * 60;

        TimeLabel.Text = _prepaid ? L.T("TIEMPO RESTANTE") : L.T("TIEMPO TRANSCURRIDO");
        AmountText.Text = $"$ {session.TotalAmount:N2}";
        AmountBsText.Text = $"Bs {session.TotalAmount * state.ExchangeRate:N2}";

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
        AccountTimeBsText.Text = $"Bs {session.TimeAmount * state.ExchangeRate:N2}";
        AccountConsumptionUsdText.Text = $"$ {session.ConsumptionAmount:N2}";
        AccountConsumptionBsText.Text = $"Bs {session.ConsumptionAmount * state.ExchangeRate:N2}";
        AccountTotalUsdText.Text = $"$ {session.TotalAmount:N2}";
        AccountTotalBsText.Text = $"Bs {session.TotalAmount * state.ExchangeRate:N2}";
        AccountRateText.Text = $"1 USD = {state.ExchangeRate:N2} Bs";

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

    private void Tick()
    {
        var session = _state?.Session;
        if (session is null) return;

        var delta = (int)Math.Max(0, (DateTime.UtcNow - _baseAt).TotalSeconds);
        var accent = (Brush)FindResource("AccentBrush");
        var danger = (Brush)FindResource("DangerBrush");
        var warning = (Brush)FindResource("WarningBrush");

        if (_prepaid && session.Type != SessionType.Free)
        {
            var remaining = _baseSeconds - delta;
            if (remaining >= 0)
            {
                TimeText.Text = Format(remaining);
                TimeText.Foreground = remaining <= 300 ? warning : accent;
                WarningText.Visibility = _warningText is null ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                TimeText.Text = "+" + Format(-remaining);
                TimeText.Foreground = danger;
                WarningText.Text = L.T("¡Tiempo agotado!");
                WarningText.Visibility = Visibility.Visible;
            }
        }
        else
        {
            TimeText.Text = Format(_baseSeconds + delta);
            TimeText.Foreground = accent;
            if (_overtime)
            {
                TimeText.Foreground = danger;
                WarningText.Text = L.T("¡Tiempo agotado!");
                WarningText.Visibility = Visibility.Visible;
            }
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
        Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2);
        Top = 8;
    }

    private void Order_Click(object sender, RoutedEventArgs e) => OrderRequested?.Invoke();

    private void Account_Click(object sender, RoutedEventArgs e)
    {
        AccountPopup.IsOpen = !AccountPopup.IsOpen;
    }
}

