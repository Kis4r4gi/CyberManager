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
    private readonly DispatcherTimer _clockTimer =
        new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
    private int _lastClockSecond = -1;
    private KeyboardBlocker? _blocker;
    private ImageSource? _wallpaper;
    private string? _promoPath;

    public bool AllowClose { get; set; }
    /// <summary>Evita que la pantalla recupere el foco mientras hay un diálogo abierto encima.</summary>
    public bool SuppressActivation { get; set; }
    /// <summary>El menú flotante de inicio de sesión está abierto.</summary>
    public bool IsLoginMenuOpen => LoginPopup.IsOpen;
    /// <summary>El usuario eligió iniciar sesión con una cuenta prepago.</summary>
    public event Action? PrepaidLoginRequested;
    /// <summary>El usuario eligió iniciar sesión como administrador.</summary>
    public event Action? AdminLoginRequested;
    /// <summary>El video promocional terminó; la aplicación debe avanzar al siguiente.</summary>
    public event Action? PromoVideoEnded;
    /// <summary>El video no se pudo reproducir (códec ausente o archivo dañado).</summary>
    public event Action? PromoVideoFailed;

    public LockWindow()
    {
        InitializeComponent();
        VersionText.Text = $"{L.T("Versión")} {AppInfo.Version}";

        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            UpdateClock();
            _clockTimer.Start();
            _blocker ??= new KeyboardBlocker();
        };
        Closed += (_, _) =>
        {
            _clockTimer.Stop();
            _blocker?.Dispose();
        };
        Closing += (_, e) =>
        {
            if (!AllowClose) e.Cancel = true;
        };
        Deactivated += (_, _) =>
        {
            // No se reactiva encima del menú de login ni de un diálogo modal:
            // hacerlo robaba el foco y cerraba el menú.
            if (AllowClose || SuppressActivation || IsLoginMenuOpen) return;
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
            BusinessNameText.Text = "CyberManager";
            FooterText.Text = string.Empty;
            TitleText.Text = L.T("EQUIPO BLOQUEADO");
            MessageText.Text = L.T("No hay conexión con el servidor. Avise al administrador.");
            LoginPanel.Visibility = Visibility.Visible;
            return;
        }

        // El reloj de la pantalla usa la hora local del equipo (Windows).
        UpdateClock();

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
            LoginPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            TitleText.Text = L.T("EQUIPO BLOQUEADO");
            MessageText.Text = L.T("Solicite al cajero o inicie sesión para empezar.");
            LoginPanel.Visibility = Visibility.Visible;
        }
    }

    public void ShowSystemMessage(string message)
    {
        TitleText.Text = "SISTEMA";
        MessageText.Text = message;
        LoginPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Activa o libera el bloqueo de atajos del sistema. Debe reflejar si la
    /// pantalla de bloqueo está visible, no el ciclo de vida de la ventana.
    /// </summary>
    public void SetKeyboardBlocked(bool blocked)
    {
        if (blocked)
        {
            _blocker ??= new KeyboardBlocker();
        }
        else
        {
            _blocker?.Dispose();
            _blocker = null;
        }
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

    /// <summary>Libera el teclado y oculta la pantalla de bloqueo mientras el administrador
    /// revisa la configuración del cliente.</summary>
    public void EnterAdminMode()
    {
        LoginPopup.IsOpen = false;
        SetKeyboardBlocked(false);
        AllowClose = true;
        Hide();
    }

    /// <summary>Vuelve a bloquear el equipo cuando el administrador cierra la configuración.</summary>
    public void ExitAdminMode()
    {
        AllowClose = false;
        Show();
        Activate();
        SetKeyboardBlocked(true);
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

    private void Login_Click(object sender, RoutedEventArgs e)
        => LoginPopup.IsOpen = !LoginPopup.IsOpen;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !LoginPopup.IsOpen) return;
        LoginPopup.IsOpen = false;
        e.Handled = true;
    }

    private void Popup_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        LoginPopup.IsOpen = false;
        e.Handled = true;
    }

    private void PrepaidLogin_Click(object sender, RoutedEventArgs e)
    {
        LoginPopup.IsOpen = false;
        PrepaidLoginRequested?.Invoke();
    }

    private void AdminLogin_Click(object sender, RoutedEventArgs e)
    {
        LoginPopup.IsOpen = false;
        AdminLoginRequested?.Invoke();
    }

    private void UpdateClock()
    {
        // Reloj de 12 horas con AM/PM usando la hora local del equipo (Windows).
        // Se consulta varias veces por segundo para que el segundero no se salte
        // cuando el hilo de interfaz está ocupado, pero solo se redibuja al cambiar.
        var now = DateTime.Now;
        if (now.Second == _lastClockSecond) return;
        _lastClockSecond = now.Second;

        var culture = L.IsEnglish ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("es-VE");
        ClockText.Text = now.ToString("h:mm:ss tt", culture);
        DateText.Text = Capitalize(now.ToString("dddd, d 'de' MMMM 'de' yyyy", culture));
    }

    private static string Capitalize(string text)
        => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text[1..];
}
