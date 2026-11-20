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
    private KeyboardBlocker? _blocker;
    private ImageSource? _wallpaper;
    private string? _promoPath;

    // Diagnóstico del video: compara la posición del medio con el tiempo real para
    // medir si se reproduce en cámara lenta. Se reporta en el latido del cliente.
    private readonly DispatcherTimer _promoDiagTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private DateTime _promoStartedUtc;

    // Fluidez: cuenta los fotogramas que WPF compone mientras hay video. Sirve para
    // comparar la tasa real con la del archivo; WPF tiende a topar alrededor de 30 fps.
    private int _renderFrames;
    private DateTime _renderFpsSince = DateTime.UtcNow;
    private double _renderFps;
    private bool _renderHooked;

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

        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            _blocker ??= new KeyboardBlocker();
            ApplyRenderCache();
        };
        _promoDiagTimer.Tick += (_, _) => UpdateMediaDiagnostic();
        Closed += (_, _) =>
        {
            _blocker?.Dispose();
            StopRenderProbe();
            _promoDiagTimer.Stop();
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

    /// <summary>
    /// Cachea el velo y el texto como mapas de bits. Son elementos estáticos que el
    /// hilo de render volvía a rasterizar en cada fotograma del video (el contorno se
    /// dibuja con varias pasadas), y en equipos modestos esa carga resta CPU a la
    /// decodificación: el video se ve en cámara lenta o a tirones. Al cachearlos, el
    /// video solo compone imágenes ya listas.
    /// </summary>
    private void ApplyRenderCache()
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale <= 0) scale = 1;

        FrameworkElement[] staticLayers =
        {
            OverlayVeil, BusinessNameText, CenterStationText, ZoneText,
            TitleText, MessageText, FooterText, VersionText
        };

        foreach (var element in staticLayers)
            element.CacheMode = new BitmapCache(scale);
    }

    public void UpdateState(StationClientStateDto? state)
    {
        if (state is null)
        {
            CenterStationText.Text = $"PC-{App.Config.StationId:D2}";
            ZoneText.Text = L.T("Sin conexión");
            BusinessNameText.Text = "CyberManager";
            FooterText.Text = string.Empty;
            TitleText.Text = L.T("EQUIPO BLOQUEADO");
            MessageText.Text = L.T("No hay conexión con el servidor. Avise al administrador.");
            LoginPanel.Visibility = Visibility.Visible;
            return;
        }

        BusinessNameText.Text = state.BusinessName;
        CenterStationText.Text = state.Name;
        ZoneText.Text = state.ZoneName ?? L.T("Estación");
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

    /// <summary>Muestra el fondo publicado por el servidor con la transición configurada.</summary>
    public void SetWallpaper(ImageSource? image, WallpaperTransition transition = WallpaperTransition.Fade)
    {
        if (image is null)
        {
            WallpaperImage.BeginAnimation(OpacityProperty, null);
            WallpaperImage.BeginAnimation(RenderTransformProperty, null);
            WallpaperImage.RenderTransform = null;
            WallpaperImage.Source = null;
            WallpaperImage.Visibility = Visibility.Collapsed;
            WallpaperImage.Opacity = 0;
            _wallpaper = null;
            return;
        }

        // Mientras se reproduce un video, la imagen queda oculta: manda el video.
        if (_promoPath is not null) return;

        if (ReferenceEquals(_wallpaper, image) && WallpaperImage.Source is not null) return;
        _wallpaper = image;

        WallpaperImage.Source = image;
        WallpaperImage.Visibility = Visibility.Visible;

        // Limpia animaciones y transformaciones de la imagen anterior.
        WallpaperImage.BeginAnimation(OpacityProperty, null);
        WallpaperImage.BeginAnimation(RenderTransformProperty, null);
        WallpaperImage.RenderTransform = null;
        WallpaperImage.RenderTransformOrigin = new Point(0.5, 0.5);

        var duration = TimeSpan.FromMilliseconds(450);

        switch (transition)
        {
            case WallpaperTransition.None:
                WallpaperImage.Opacity = 1;
                break;

            case WallpaperTransition.Slide:
            {
                var transform = new TranslateTransform();
                WallpaperImage.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(80, 0, duration)
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                WallpaperImage.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, duration)
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                break;
            }

            case WallpaperTransition.Zoom:
            {
                var transform = new ScaleTransform(1.08, 1.08);
                WallpaperImage.RenderTransform = transform;
                transform.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(1.08, 1, duration)
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(1.08, 1, duration)
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                WallpaperImage.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, duration)
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                break;
            }

            default:
                WallpaperImage.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, duration)
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                break;
        }
    }

    /// <summary>Reproduce el video promocional indicado. <c>null</c> detiene la reproducción.</summary>
    public void SetPromoVideo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _promoPath = null;
            _promoDiagTimer.Stop();
            StopRenderProbe();
            ClientDiagnostics.Media = null;
            try { PromoVideo.Stop(); } catch { /* sin fuente */ }
            PromoVideo.Source = null;
            PromoVideo.Visibility = Visibility.Collapsed;

            // Vuelve a verse el fondo que estaba activo antes del video.
            if (_wallpaper is not null)
            {
                WallpaperImage.BeginAnimation(OpacityProperty, null);
                WallpaperImage.RenderTransform = null;
                WallpaperImage.Opacity = 1;
                WallpaperImage.Visibility = Visibility.Visible;
            }
            return;
        }

        if (string.Equals(_promoPath, path, StringComparison.OrdinalIgnoreCase) &&
            PromoVideo.Source is not null)
        {
            if (PromoVideo.Visibility != Visibility.Visible)
                PromoVideo.Visibility = Visibility.Visible;
            _promoStartedUtc = DateTime.UtcNow;
            if (!_promoDiagTimer.IsEnabled) _promoDiagTimer.Start();
            StartRenderProbe();
            try { PromoVideo.Play(); } catch { /* se reintentará */ }
            return;
        }

        _promoPath = path;
        // El video se muestra encima: oculta la imagen para que no lo tape.
        WallpaperImage.Visibility = Visibility.Collapsed;
        _promoStartedUtc = DateTime.UtcNow;
        _promoDiagTimer.Start();
        StartRenderProbe();
        try
        {
            PromoVideo.Stop();
            PromoVideo.Source = new Uri(path, UriKind.Absolute);
            PromoVideo.Visibility = Visibility.Visible;
            PromoVideo.Play();
        }
        catch (Exception ex)
        {
            ClientLog.Error($"No se pudo iniciar el video {path}", ex);
            PromoVideo.Visibility = Visibility.Collapsed;
            PromoVideoFailed?.Invoke();
        }
    }

    /// <summary>
    /// Reporta si el video avanza a tiempo real y a qué tasa lo compone WPF. El texto
    /// es deliberadamente estable (sin la posición exacta) para que el servidor solo
    /// lo registre cuando hay un cambio apreciable y no en cada latido.
    /// </summary>
    private void UpdateMediaDiagnostic()
    {
        if (_promoPath is null)
        {
            ClientDiagnostics.Media = null;
            return;
        }

        var wall = DateTime.UtcNow - _promoStartedUtc;
        var position = TimeSpan.Zero;
        try { position = PromoVideo.Position; }
        catch { /* sin fuente */ }

        // Menos de 1 s de reloj no da una proporción fiable.
        var ratio = wall.TotalSeconds >= 1 ? position.TotalSeconds / wall.TotalSeconds : 1;

        var elapsed = (DateTime.UtcNow - _renderFpsSince).TotalSeconds;
        if (elapsed >= 1)
        {
            _renderFps = _renderFrames / elapsed;
            _renderFrames = 0;
            _renderFpsSince = DateTime.UtcNow;
        }

        // Se redondea para que el valor no cambie en cada latido.
        var fpsBand = (int)Math.Round(_renderFps / 5) * 5;
        var ratioBand = Math.Round(ratio, 1);

        ClientDiagnostics.Media =
            $"{PromoVideo.NaturalVideoWidth}x{PromoVideo.NaturalVideoHeight} " +
            $"x{ratioBand:0.0} fps~{fpsBand}";
    }

    /// <summary>Cuenta los fotogramas que WPF compone para medir la fluidez del video.</summary>
    private void StartRenderProbe()
    {
        _renderFrames = 0;
        _renderFpsSince = DateTime.UtcNow;
        if (_renderHooked) return;
        CompositionTarget.Rendering += OnCompositionRendering;
        _renderHooked = true;
    }

    private void StopRenderProbe()
    {
        if (!_renderHooked) return;
        CompositionTarget.Rendering -= OnCompositionRendering;
        _renderHooked = false;
        _renderFps = 0;
    }

    private void OnCompositionRendering(object? sender, EventArgs e) => _renderFrames++;

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

    /// <summary>
    /// Con archivos grandes, Play() puede llegar antes de que el media esté listo.
    /// Este evento garantiza que el video arranque automáticamente en cuanto abra.
    /// </summary>
    private void PromoVideo_MediaOpened(object sender, RoutedEventArgs e)
    {
        ClientLog.Info(
            $"Video abierto: {PromoVideo.NaturalVideoWidth}x{PromoVideo.NaturalVideoHeight}, " +
            $"duración {PromoVideo.NaturalDuration}, tier {RenderCapability.Tier >> 16}, " +
            $"archivo {_promoPath}");

        if (_promoPath is not null) PromoVideo.Play();
    }

    private void PromoVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        var wall = DateTime.UtcNow - _promoStartedUtc;
        var expected = PromoVideo.NaturalDuration.HasTimeSpan
            ? PromoVideo.NaturalDuration.TimeSpan.TotalSeconds
            : 0;
        ClientLog.Info($"Video terminado: real {wall.TotalSeconds:0.0}s, esperado {expected:0.0}s");

        _promoDiagTimer.Stop();
        StopRenderProbe();
        ClientDiagnostics.Media = null;

        try { PromoVideo.Position = TimeSpan.Zero; } catch { /* sin fuente */ }
        PromoVideoEnded?.Invoke();
    }

    private void PromoVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        ClientLog.Error($"El video no se pudo reproducir ({_promoPath}): {e.ErrorException?.Message}",
            e.ErrorException);
        _promoDiagTimer.Stop();
        StopRenderProbe();
        ClientDiagnostics.Media = null;
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
}
