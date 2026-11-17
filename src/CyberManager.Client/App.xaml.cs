using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Net.Http.Json;
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
    private DispatcherTimer? _updateTimer;
    private LockWindow? _lockWindow;
    private SessionBarWindow? _barWindow;
    private OrderWindow? _orderWindow;
    private InputBlocker? _inputBlocker;
    private bool _forcedLock;
    private bool _shuttingDown;
    private bool _running;
    private bool _restarting;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private DateTime _lastConnectAttempt = DateTime.MinValue;
    private DiscoveryResponder? _discovery;
    private SetupWindow? _setupWindow;
    private PrepaidWindow? _prepaidWindow;
    private bool _adminMode;

    private static readonly HttpClient UpdateHttp = new() { Timeout = TimeSpan.FromMinutes(3) };

    private static bool Configured =>
        Config.StationId > 0 && (!string.IsNullOrWhiteSpace(Config.StationToken) ||
            (!string.IsNullOrWhiteSpace(Config.StationIpAddress) && !string.IsNullOrWhiteSpace(Config.StationMacAddress)));

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

        // El descubrimiento debe estar activo incluso antes de configurar el equipo:
        // así el servidor puede encontrarlo y enviarle el ID y el token.
        StartDiscovery();

        if (!Configured)
        {
            _setupWindow = new SetupWindow();
            var accepted = _setupWindow.ShowDialog() == true;
            _setupWindow = null;
            if (!accepted)
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

        // El token es la credencial principal; la IP/MAC se mantienen como respaldo
        // para equipos instalados antes de que el servidor asignara tokens.
        Api = string.IsNullOrWhiteSpace(Config.StationToken)
            ? new StationApi(Config.ServerUrl, Config.StationId,
                Config.StationIpAddress ?? string.Empty, Config.StationMacAddress ?? string.Empty)
            : new StationApi(Config.ServerUrl, Config.StationId, Config.StationToken);

        Realtime = new StationRealtime();
        Realtime.CommandReceived += OnCommandReceived;
        Realtime.StationStateChanged += OnStationStateChanged;
        Realtime.SessionWarning += OnSessionWarning;
        Realtime.WallpaperChanged += OnWallpaperChanged;
        Realtime.PromoVideosChanged += OnPromoVideosChanged;
        Realtime.ScreenFrameRequested += OnScreenFrameRequested;
        Realtime.RemoteInputReceived += OnRemoteInputReceived;
        Realtime.ClientUpdateAvailable += OnClientUpdateAvailable;

        try
        {
            // Conectar usando el método apropiado según la configuración
            if (!string.IsNullOrWhiteSpace(Config.StationToken))
            {
                await Realtime.ConnectAsync(Api.BaseUrl, Config.StationId, Config.StationToken);
            }
            else
            {
                await Realtime.ConnectAsync(Api.BaseUrl, Config.StationId,
                    Config.StationIpAddress ?? string.Empty, Config.StationMacAddress ?? string.Empty);
            }
            _lastConnectAttempt = DateTime.UtcNow;
        }
        catch
        {
            // El servidor puede estar apagado al arrancar; se reintenta en cada refresco.
        }

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _updateTimer.Tick += async (_, _) => await CheckClientUpdateAsync();
        _updateTimer.Start();

        await RefreshAsync();
        await CheckClientUpdateAsync();
        _running = true;
    }

    private void StartDiscovery()
    {
        if (_discovery is not null) return;
        try
        {
            _discovery = new DiscoveryResponder();
            _discovery.Configured += OnDiscoveryConfigured;
        }
        catch
        {
            // El puerto UDP puede estar ocupado; el equipo seguirá funcionando con
            // la configuración manual.
            _discovery = null;
        }
    }

    private void OnDiscoveryConfigured(DiscoveryMessage message)
        => Dispatcher.BeginInvoke(async () =>
        {
            Config = AppConfig.Load("Client");

            // Si la ventana de configuración está abierta, se cierra para aplicar
            // los datos que envió el administrador.
            if (_setupWindow is { IsVisible: true } setup)
            {
                setup.DialogResult = true;
                setup.Close();
                return;
            }

            if (_running) await StartAsync();
        });

    private void OnClientUpdateAvailable(ClientUpdateManifestDto manifest)
        => Dispatcher.BeginInvoke(async () => await ApplyClientUpdateAsync(manifest));

    /// <summary>
    /// Consulta el manifiesto del servidor cada 10 minutos. El agente suele aplicar la
    /// actualización como SYSTEM; si el cliente puede escribir su carpeta, la aplica él.
    /// </summary>
    private async Task CheckClientUpdateAsync()
    {
        if (_restarting || Api is null) return;
        if (DateTime.UtcNow - _lastUpdateCheck < TimeSpan.FromMinutes(2)) return;
        _lastUpdateCheck = DateTime.UtcNow;

        try
        {
            var manifest = await UpdateHttp.GetFromJsonAsync<ClientUpdateManifestDto>(
                $"{Api.BaseUrl.TrimEnd('/')}/api/client-update/manifest");
            if (manifest is not null) await ApplyClientUpdateAsync(manifest);
        }
        catch
        {
            // Sin conexión o sin paquete: se reintenta en la próxima consulta.
        }
    }

    private async Task ApplyClientUpdateAsync(ClientUpdateManifestDto manifest)
    {
        if (_restarting || string.IsNullOrWhiteSpace(manifest.Version)) return;
        if (!VersionUtil.IsNewer(manifest.Version, AppInfo.Version)) return;

        var clientDir = ClientUpdateInstaller.ResolveClientDir();
        if (clientDir is null) return;

        try
        {
            var workDir = Path.Combine(Path.GetTempPath(), "CyberManager", "ClientUpdate",
                manifest.Version);
            var staging = await ClientUpdateInstaller.DownloadAndExtractAsync(
                Api?.BaseUrl ?? Config.ServerUrl, manifest, workDir, UpdateHttp, CancellationToken.None);

            // El SHA-256 no coincide: no se aplica nada.
            if (staging is null) return;

            if (ClientUpdateInstaller.Apply(staging, clientDir, out _))
                RestartForUpdate();
        }
        catch
        {
            // Sin permisos de escritura el agente aplicará la actualización.
        }
    }

    /// <summary>Reinicia la pantalla para cargar los binarios nuevos y quita la marca.</summary>
    private void RestartForUpdate()
    {
        if (_restarting) return;
        _restarting = true;

        ClientUpdateInstaller.TryDelete(Path.Combine(AppContext.BaseDirectory,
            ClientUpdateInstaller.PendingFile));

        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = AppContext.BaseDirectory
                });
            }
        }
        catch
        {
            // Si no se puede reiniciar, el usuario lo hará al abrir el equipo.
        }

        Shutdown();
    }

    private async Task StopAsync()
    {
        _pollTimer?.Stop();
        _pollTimer = null;

        _updateTimer?.Stop();
        _updateTimer = null;

        if (Realtime is not null)
        {
            Realtime.CommandReceived -= OnCommandReceived;
            Realtime.StationStateChanged -= OnStationStateChanged;
            Realtime.SessionWarning -= OnSessionWarning;
            Realtime.WallpaperChanged -= OnWallpaperChanged;
            Realtime.PromoVideosChanged -= OnPromoVideosChanged;
            Realtime.ScreenFrameRequested -= OnScreenFrameRequested;
            Realtime.RemoteInputReceived -= OnRemoteInputReceived;
            Realtime.ClientUpdateAvailable -= OnClientUpdateAvailable;
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

        // Si el agente reemplazó los archivos, se reinicia con la versión nueva.
        if (!_restarting && File.Exists(Path.Combine(AppContext.BaseDirectory,
                ClientUpdateInstaller.PendingFile)))
        {
            RestartForUpdate();
            return;
        }

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
                // Reintentar conexión usando el método apropiado
                if (!string.IsNullOrWhiteSpace(Config.StationToken))
                {
                    await Realtime.ConnectAsync(Api.BaseUrl, Config.StationId, Config.StationToken);
                }
                else
                {
                    await Realtime.ConnectAsync(Api.BaseUrl, Config.StationId,
                        Config.StationIpAddress ?? string.Empty, Config.StationMacAddress ?? string.Empty);
                }
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
            AppInfo.Version,
            Environment.OSVersion.VersionString,
            0,
            0,
            IsLocked ? "locked" : "unlocked");

        return Realtime.HeartbeatAsync(heartbeat);
    }

    private void ApplyState(StationClientStateDto state)
    {
        // Mientras el administrador revisa la configuración, la pantalla de bloqueo
        // permanece oculta y no se vuelve a bloquear el equipo.
        if (_adminMode)
        {
            _lockWindow?.UpdateState(state);
            return;
        }

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
            _lockWindow.PrepaidLoginRequested += OnPrepaidLoginRequested;
            _lockWindow.AdminLoginRequested += OnAdminLoginRequested;
            _lockWindow.PromoVideoEnded += () => _promos.Next();
            _lockWindow.PromoVideoFailed += () => _promos.Next();
        }

        _lockWindow.UpdateState(state);
        if (!_lockWindow.IsVisible) _lockWindow.Show();

        // RefreshAsync se ejecuta cada 5 segundos. Activar la pantalla de bloqueo en
        // cada refresco le robaba el foco a los cuadros de texto del login y cerraba
        // el menú flotante, así que solo se activa cuando no hay un diálogo encima.
        if (!_lockWindow.IsActive && _prepaidWindow is null && !_adminMode &&
            !_lockWindow.SuppressActivation && !_lockWindow.IsLoginMenuOpen)
        {
            _lockWindow.Activate();
        }

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
            _barWindow.RechargeRequested += OnPrepaidRechargeRequested;
            _barWindow.FinishRequested += OnPrepaidFinishRequested;
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

    /// <summary>Autoservicio prepago desde la pantalla de bloqueo.</summary>
    private void OnPrepaidLoginRequested() => OpenPrepaidWindow();

    /// <summary>Inicio de sesión de administrador para editar la configuración del cliente.</summary>
    private async void OnAdminLoginRequested()
    {
        if (_lockWindow is null) return;

        _lockWindow.SuppressActivation = true;
        AdminLoginWindow login;
        try
        {
            login = new AdminLoginWindow();
            if (_lockWindow.IsVisible) login.Owner = _lockWindow;
            if (login.ShowDialog() != true || login.Api is null) return;
        }
        finally
        {
            _lockWindow.SuppressActivation = false;
        }

        _adminMode = true;
        _lockWindow.EnterAdminMode();

        var settings = new ClientSettingsWindow(login.Api);
        try
        {
            settings.ShowDialog();
        }
        finally
        {
            _adminMode = false;

            // Si el administrador pidió desinstalar, la aplicación se cierra sin
            // volver a mostrar la pantalla de bloqueo.
            if (!settings.UninstallRequested)
            {
                _lockWindow.ExitAdminMode();

                // El idioma se guarda solo en este equipo: se aplica a las ventanas abiertas.
                L.SetLanguage(Config.Language);
                Loc.Apply(_lockWindow);
                if (_barWindow is not null) Loc.Apply(_barWindow);
            }
        }

        if (settings.UninstallRequested)
        {
            Shutdown();
            return;
        }

        await RefreshAsync();
    }

    private void OpenPrepaidWindow()
    {
        if (_prepaidWindow is { IsLoaded: true })
        {
            _prepaidWindow.Activate();
            return;
        }

        if (_lockWindow is not null) _lockWindow.SuppressActivation = true;

        var window = new PrepaidWindow();
        if (_lockWindow is { IsVisible: true }) window.Owner = _lockWindow;
        _prepaidWindow = window;
        window.SessionStarted += () => _ = RefreshAsync();
        window.Closed += (_, _) =>
        {
            _prepaidWindow = null;
            if (_lockWindow is not null)
            {
                _lockWindow.SuppressActivation = false;
                if (_lockWindow.IsVisible) _lockWindow.Activate();
            }
            _ = RefreshAsync();
        };

        window.ShowDialog();
    }

    private void OnPrepaidRechargeRequested() => OpenPrepaidWindow();

    private async void OnPrepaidFinishRequested()
    {
        if (Api is null) return;

        var question = L.IsEnglish
            ? "Finish the prepaid session on this station? Unused minutes return to the account."
            : "¿Finalizar la sesión prepago en este equipo? Los minutos no usados vuelven a la cuenta.";
        var owner = _barWindow is { IsVisible: true } ? (Window?)_barWindow : _lockWindow;
        var confirmed = owner is not null
            ? MessageBox.Show(owner, question, L.T("Finalizar sesión"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
            : MessageBox.Show(question, L.T("Finalizar sesión"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (!confirmed) return;

        try
        {
            var result = await Api.PrepaidFinishAsync();
            await RefreshAsync();

            var message = L.IsEnglish
                ? $"Session finished. {result.RefundedMinutes} unused minutes returned to the account."
                : $"Sesión finalizada. {result.RefundedMinutes} minutos no usados volvieron a la cuenta.";
            if (owner is not null)
                MessageBox.Show(owner, message, L.T("Finalizar sesión"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(message, L.T("Finalizar sesión"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            if (owner is not null)
                MessageBox.Show(owner, ex.Message, L.T("Finalizar sesión"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show(ex.Message, L.T("Finalizar sesión"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
        _running = false;

        if (_discovery is not null)
        {
            _discovery.Configured -= OnDiscoveryConfigured;
            _discovery.Dispose();
            _discovery = null;
        }

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