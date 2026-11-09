using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using CyberManager.Client.Services;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

public partial class App : Application
{
    public static AppConfig Config { get; private set; } = new();
    public static StationApi? Api { get; private set; }
    public static StationRealtime? Realtime { get; private set; }
    public static StationClientStateDto? State { get; private set; }
    public static bool IsLocked { get; private set; } = true;

    private readonly WallpaperService _wallpapers = new();
    private readonly PromoVideoService _promos = new();

    private DispatcherTimer? _pollTimer;
    private LockWindow? _lockWindow;
    private SessionBarWindow? _barWindow;
    private OrderWindow? _orderWindow;
    private InputBlocker? _inputBlocker;
    private bool _forcedLock;
    private bool _shuttingDown;
    private DateTime _lastConnectAttempt = DateTime.MinValue;

    private static bool Configured =>
        Config.StationId > 0 && !string.IsNullOrWhiteSpace(Config.StationToken);

    public App()
    {
        _wallpapers.ImageChanged += source => _lockWindow?.SetWallpaper(source);
        _promos.VideoChanged += path => _lockWindow?.SetPromoVideo(path);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Config = AppConfig.Load("Client");
        L.SetLanguage(Config.Language);

        if (!Configured)
        {
            var setup = new SetupWindow();
            if (setup.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            Config = AppConfig.Load("Client");
        }

        if (!Configured)
        {
            Shutdown();
            return;
        }

        try
        {
            await StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo iniciar la estación:\n\n{ex.Message}", "CyberManager",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task StartAsync()
    {
        await StopAsync();

        Api = new StationApi(Config.ServerUrl, Config.StationId, Config.StationToken);
        Realtime = new StationRealtime();
        Realtime.CommandReceived += OnCommandReceived;
        Realtime.StationStateChanged += OnStationStateChanged;
        Realtime.SessionWarning += OnSessionWarning;
        Realtime.WallpaperChanged += OnWallpaperChanged;
        Realtime.PromoVideosChanged += OnPromoVideosChanged;
        Realtime.ScreenFrameRequested += OnScreenFrameRequested;
        Realtime.RemoteInputReceived += OnRemoteInputReceived;

        try
        {
            await Realtime.ConnectAsync(Api.BaseUrl, Config.StationId, Config.StationToken);
            _lastConnectAttempt = DateTime.UtcNow;
        }
        catch
        {
            // El servidor puede estar apagado al arrancar; se reintenta en cada refresco.
        }

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();

        await RefreshAsync();
    }

    private async Task StopAsync()
    {
        _pollTimer?.Stop();
        _pollTimer = null;

        if (Realtime is not null)
        {
            Realtime.CommandReceived -= OnCommandReceived;
            Realtime.StationStateChanged -= OnStationStateChanged;
            Realtime.SessionWarning -= OnSessionWarning;
            Realtime.WallpaperChanged -= OnWallpaperChanged;
            Realtime.PromoVideosChanged -= OnPromoVideosChanged;
            Realtime.ScreenFrameRequested -= OnScreenFrameRequested;
            Realtime.RemoteInputReceived -= OnRemoteInputReceived;
            await Realtime.DisposeAsync();
            Realtime = null;
        }

        _wallpapers.SetLocked(false);
        _promos.SetLocked(false);
        SetInputBlocked(false);

        CloseOrder();

        if (_barWindow is not null)
        {
            _barWindow.Close();
            _barWindow = null;
        }

        if (_lockWindow is not null)
        {
            _lockWindow.AllowClose = true;
            _lockWindow.Close();
            _lockWindow = null;
        }
    }

    private async Task RefreshAsync()
    {
        if (Api is null) return;

        try
        {
            var state = await Api.StateAsync();
            State = state;
            ApplyState(state);
            SessionBackupService.Save(state);
            await SendHeartbeatAsync();
        }
        catch
        {
            // Sin conexión con el servidor: si el equipo acaba de iniciar, se
            // restaura el último tiempo guardado para no perder la sesión.
            if (State is null)
            {
                var restored = SessionBackupService.TryRestore(Config.StationId);
                if (restored is not null)
                {
                    State = restored;
                    ApplyState(restored);
                    _barWindow?.ShowRestoredBackup();
                }
            }
        }

        if (Realtime is not null && !Realtime.IsConnected &&
            DateTime.UtcNow - _lastConnectAttempt > TimeSpan.FromSeconds(20))
        {
            _lastConnectAttempt = DateTime.UtcNow;
            try
            {
                await Realtime.ConnectAsync(Api.BaseUrl, Config.StationId, Config.StationToken);
            }
            catch { /* se reintenta luego */ }
        }
    }

    private Task SendHeartbeatAsync()
    {
        if (Realtime is null || !Realtime.IsConnected) return Task.CompletedTask;

        var heartbeat = new AgentHeartbeat(
            Config.StationId,
            Environment.MachineName,
            Environment.UserName,
            "1.0.0",
            Environment.OSVersion.VersionString,
            0,
            0,
            IsLocked ? "locked" : "unlocked");

        return Realtime.HeartbeatAsync(heartbeat);
    }

    private void ApplyState(StationClientStateDto state)
    {
        var sessionActive = state.Session is not null && state.Session.Status != SessionStatus.Paused;
        SetLocked(_forcedLock || !sessionActive, state);
    }

    private void SetLocked(bool locked, StationClientStateDto? state)
    {
        if (_shuttingDown) return;
        IsLocked = locked;

        if (locked)
        {
            ShowLock(state);
        }
        else
        {
            ShowBar(state);
        }
    }

    private void ShowLock(StationClientStateDto? state)
    {
        CloseOrder();

        if (_barWindow is not null)
        {
            _barWindow.Hide();
        }

        if (_lockWindow is null)
        {
            _lockWindow = new LockWindow();
            _lockWindow.SetupRequested += OnSetupRequested;
            _lockWindow.PromoVideoEnded += () => _promos.Next();
            _lockWindow.PromoVideoFailed += () => _promos.Next();
        }

        _lockWindow.UpdateState(state);
        if (!_lockWindow.IsVisible) _lockWindow.Show();
        _lockWindow.Activate();

        _wallpapers.SetLocked(true);
        _ = _wallpapers.ApplyAsync(state?.Wallpaper);
        _promos.SetLocked(true);
        _ = _promos.ApplyAsync(state?.Promos);
    }

    private void ShowBar(StationClientStateDto? state)
    {
        _wallpapers.SetLocked(false);
        _promos.SetLocked(false);

        if (_lockWindow is not null)
        {
            _lockWindow.Hide();
        }

        if (_barWindow is null)
        {
            _barWindow = new SessionBarWindow();
            _barWindow.OrderRequested += OpenOrder;
        }

        _barWindow.UpdateState(state ?? State ?? new StationClientStateDto(
            Config.StationId, $"PC-{Config.StationId:D2}", null, false, StationStatus.Available,
            null, 0, "CyberManager", "Bs", null, DateTime.UtcNow));

        if (!_barWindow.IsVisible) _barWindow.Show();
    }

    private void OnCommandReceived(AgentCommand command)
        => Dispatcher.BeginInvoke(() => HandleCommand(command));

    private async void HandleCommand(AgentCommand command)
    {
        switch (command.Command)
        {
            case AgentCommandType.Lock:
                _forcedLock = true;
                SetLocked(true, State);
                break;

            case AgentCommandType.Unlock:
                _forcedLock = false;
                SetLocked(false, State);
                await RefreshAsync();
                break;

            case AgentCommandType.Message:
                ShowInfo(command.Argument ?? "Mensaje del administrador.");
                break;

            case AgentCommandType.Shutdown:
            case AgentCommandType.Restart:
            case AgentCommandType.Logoff:
                ShowSystemShutdown(command.Command);
                break;

            case AgentCommandType.SetVolume:
                if (int.TryParse(command.Argument, out var percent))
                    AudioControl.SetVolume(percent);
                break;

            case AgentCommandType.Mute:
                AudioControl.SetMute(true);
                break;

            case AgentCommandType.Unmute:
                AudioControl.SetMute(false);
                break;

            case AgentCommandType.LockInput:
                SetInputBlocked(true);
                break;

            case AgentCommandType.UnlockInput:
                SetInputBlocked(false);
                break;

            case AgentCommandType.MonitorOff:
                MonitorControl.TurnOff();
                break;

            case AgentCommandType.MonitorOn:
                MonitorControl.TurnOn();
                break;

            case AgentCommandType.KillProcess:
                KillProcess(command.Argument);
                break;
        }
    }

    private void SetInputBlocked(bool blocked)
    {
        if (blocked)
        {
            _inputBlocker ??= new InputBlocker();
        }
        else
        {
            _inputBlocker?.Dispose();
            _inputBlocker = null;
        }
    }

    private static void KillProcess(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var bare = name.Trim();
        if (bare.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            bare = bare[..^4];
        if (bare.Length == 0) return;

        foreach (var process in Process.GetProcessesByName(bare))
        {
            try { process.Kill(true); } catch { /* proceso protegido o ya cerrado */ }
        }
    }

    private void OnStationStateChanged(StationStateEvent state)
        => Dispatcher.BeginInvoke(async () => await RefreshAsync());

    private void OnWallpaperChanged(WallpaperPlaylistDto playlist)
        => Dispatcher.BeginInvoke(async () => await _wallpapers.ApplyAsync(playlist));

    private void OnPromoVideosChanged(PromoVideoPlaylistDto playlist)
        => Dispatcher.BeginInvoke(async () => await _promos.ApplyAsync(playlist));

    /// <summary>Responde a la solicitud de captura del visualizador remoto de administración.</summary>
    private void OnScreenFrameRequested(ScreenFrameRequest request)
    {
        if (request.StationId != Config.StationId) return;
        _ = SendScreenFrameAsync(request);
    }

    private async Task SendScreenFrameAsync(ScreenFrameRequest request)
    {
        if (Realtime is null) return;

        ScreenFrameResult frame;
        try
        {
            var capture = await Task.Run(() => ScreenCapture.CaptureJpeg(request.MaxWidth, request.Quality));
            frame = new ScreenFrameResult(Config.StationId, request.RequestId,
                Convert.ToBase64String(capture.Jpeg), capture.Width, capture.Height,
                DateTime.UtcNow, null);
        }
        catch (Exception ex)
        {
            frame = new ScreenFrameResult(Config.StationId, request.RequestId,
                null, 0, 0, DateTime.UtcNow, ex.Message);
        }

        try
        {
            await Realtime.SendScreenFrameAsync(frame);
        }
        catch
        {
            // Si la conexión se perdió, la consola mostrará el error y podrá reintentar.
        }
    }

    /// <summary>Inyecta el mouse o teclado que envía el control remoto de administración.</summary>
    private void OnRemoteInputReceived(RemoteInputEvent input)
    {
        if (input.StationId != Config.StationId) return;
        Dispatcher.BeginInvoke(() => RemoteInputInjector.Handle(input));
    }

    private void OnSessionWarning(SessionWarningEvent warning)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try { SystemSounds.Exclamation.Play(); } catch { /* sin audio */ }

            if (warning.Overtime)
            {
                _forcedLock = true;
                SetLocked(true, State);
            }

            _barWindow?.ShowWarning(warning);
        });
    }

    private void OnSetupRequested()
    {
        _pollTimer?.Stop();

        var setup = new SetupWindow { Owner = _lockWindow };
        var saved = setup.ShowDialog() == true;

        if (saved)
        {
            Config = AppConfig.Load("Client");
            _ = StartAsync();
        }
        else
        {
            _pollTimer?.Start();
        }
    }

    private void OpenOrder()
    {
        if (_orderWindow is { IsLoaded: true })
        {
            _orderWindow.Activate();
            return;
        }

        _orderWindow = new OrderWindow { Owner = _barWindow };
        _orderWindow.Closed += (_, _) => _orderWindow = null;
        _orderWindow.ShowDialog();
    }

    private void CloseOrder()
    {
        if (_orderWindow is null) return;
        try { _orderWindow.Close(); } catch { /* ya cerrada */ }
        _orderWindow = null;
    }

    private void ShowInfo(string message)
    {
        Window? owner = _lockWindow is { IsVisible: true } ? _lockWindow : _barWindow;
        if (owner is not null)
            MessageBox.Show(owner, message, "Aviso del administrador", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show(message, "Aviso del administrador", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowSystemShutdown(AgentCommandType command)
    {
        CloseOrder();
        var text = command switch
        {
            AgentCommandType.Restart => "El equipo se está reiniciando...",
            AgentCommandType.Logoff => "La sesión de Windows se está cerrando...",
            _ => "El equipo se está apagando..."
        };

        if (_lockWindow is not null)
        {
            _lockWindow.ShowSystemMessage(text);
            _lockWindow.Show();
            _lockWindow.Activate();
        }

        _barWindow?.Hide();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _shuttingDown = true;
        await StopAsync();
        SetInputBlocked(false);
        _wallpapers.Dispose();
        _promos.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _shuttingDown = true;
        base.OnSessionEnding(e);
    }
}
