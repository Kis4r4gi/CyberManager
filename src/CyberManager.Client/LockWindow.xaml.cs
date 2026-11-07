using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

public partial class LockWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private KeyboardBlocker? _blocker;
    private ImageSource? _wallpaper;
    private string? _promoPath;

    public bool AllowClose { get; set; }
    public event Action? SetupRequested;
    /// <summary>El video promocional terminó; la aplicación debe avanzar al siguiente.</summary>
    public event Action? PromoVideoEnded;
    /// <summary>El video no se pudo reproducir (códec ausente o archivo dañado).</summary>
    public event Action? PromoVideoFailed;

    public LockWindow()
    {
        InitializeComponent();

        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            UpdateClock();
            _blocker ??= new KeyboardBlocker();
        };
        Closed += (_, _) => _blocker?.Dispose();
        Closing += (_, e) =>
        {
            if (!AllowClose) e.Cancel = true;
        };
        Deactivated += (_, _) =>
        {
            if (AllowClose) return;
            Topmost = false;
            Topmost = true;
            Activate();
        };
    }

    public void UpdateState(StationClientStateDto? state)
    {
        if (state is null)
        {
            StationText.Text = $"PC-{App.Config.StationId:D2}";
            ZoneText.Text = L.T("Sin conexión");
            RateText.Text = "1 USD = — Bs";
            MessageText.Text = L.T("No hay conexión con el servidor. Avise al administrador.");
            return;
        }

        BusinessNameText.Text = state.BusinessName;
        StationText.Text = state.Name;
        ZoneText.Text = state.ZoneName ?? L.T("Estación");
        RateText.Text = $"1 USD = {state.ExchangeRate:N2} Bs";
        FooterText.Text = state.Footer ?? string.Empty;

        if (state.Session is not null && state.Session.Status == SessionStatus.Paused)
        {
            TitleText.Text = L.T("SESIÓN EN PAUSA");
            var customer = state.Session.CustomerName ?? L.T("este equipo");
            MessageText.Text = L.IsEnglish
                ? $"The session for {customer} is paused. Ask the cashier to resume it."
                : $"La sesión de {customer} está en pausa. Solicite al cajero para reanudarla.";
        }
        else
        {
            TitleText.Text = L.T("EQUIPO BLOQUEADO");
            MessageText.Text = L.T("Solicite al cajero para iniciar su sesión.");
        }
    }

    public void ShowSystemMessage(string message)
    {
        TitleText.Text = "SISTEMA";
        MessageText.Text = message;
    }

    /// <summary>Muestra el fondo enviado por el servidor con una transición suave.</summary>
    public void SetWallpaper(ImageSource? image)
    {
        if (image is null)
        {
            WallpaperImage.BeginAnimation(OpacityProperty, null);
            WallpaperImage.Source = null;
            WallpaperImage.Visibility = Visibility.Collapsed;
            WallpaperImage.Opacity = 0;
            _wallpaper = null;
            return;
        }

        if (ReferenceEquals(_wallpaper, image) && WallpaperImage.Source is not null) return;
        _wallpaper = image;

        WallpaperImage.Source = image;
        WallpaperImage.Visibility = Visibility.Visible;
        WallpaperImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
            TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>Reproduce el video promocional indicado. <c>null</c> detiene la reproducción.</summary>
    public void SetPromoVideo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _promoPath = null;
            try { PromoVideo.Stop(); } catch { /* sin fuente */ }
            PromoVideo.Source = null;
            PromoVideo.Visibility = Visibility.Collapsed;
            return;
        }

        if (string.Equals(_promoPath, path, StringComparison.OrdinalIgnoreCase) &&
            PromoVideo.Source is not null)
        {
            if (PromoVideo.Visibility != Visibility.Visible)
                PromoVideo.Visibility = Visibility.Visible;
            try { PromoVideo.Play(); } catch { /* se reintentará */ }
            return;
        }

        _promoPath = path;
        try
        {
            PromoVideo.Stop();
            PromoVideo.Source = new Uri(path, UriKind.Absolute);
            PromoVideo.Visibility = Visibility.Visible;
            PromoVideo.Play();
        }
        catch
        {
            PromoVideo.Visibility = Visibility.Collapsed;
            PromoVideoFailed?.Invoke();
        }
    }

    private void PromoVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        try { PromoVideo.Position = TimeSpan.Zero; } catch { /* sin fuente */ }
        PromoVideoEnded?.Invoke();
    }

    private void PromoVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        PromoVideo.Visibility = Visibility.Collapsed;
        PromoVideoFailed?.Invoke();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm:ss");
        var culture = L.IsEnglish ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("es-VE");
        DateText.Text = Capitalize(now.ToString("dddd, d 'de' MMMM 'de' yyyy", culture));
    }

    private static string Capitalize(string text)
        => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text[1..];

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F12 && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            SetupRequested?.Invoke();
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
