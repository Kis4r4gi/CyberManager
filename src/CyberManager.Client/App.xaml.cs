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
    private DispatcherTimer? _keepAwakeTimer;
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
    private DateTime _lastLocateAttempt = DateTime.MinValue;
    private bool _locating;
    private DiscoveryResponder? _discovery;
    private SetupWindow? _setupWindow;
    private PrepaidWindow? _prepaidWindow;
    private bool _adminMode;
    private Mutex? _instanceMutex;
    private DateTime _startedUtc = DateTime.UtcNow;

    private static readonly HttpClient UpdateHttp = new() { Timeout = TimeSpan.FromMinutes(3) };

    /// <summary>Procesos que nunca se deben cerrar por una orden remota.</summary>
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "system idle process", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "svchost", "dwm", "explorer", "fontdrvhost",
        "lsm", "spoolsv", "audiodg", "sihost", "taskhostw", "ctfmon", "conhost",
        "cybermanager.client", "cybermanager.agent", "cybermanager.server"
    };

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
        InstallCrashHandlers();

        // Una sola pantalla por sesión de Windows: dos instancias mostrarían dos
        // bloqueos de teclado y ejecutarían cada comando dos veces. Si venimos de una
        // actualización, se espera a que el proceso anterior termine de cerrar.
        var restarting = File.Exists(Path.Combine(AppContext.BaseDirectory,
            ClientUpdateInstaller.PendingFile));
        if (!AcquireSingleInstance(restarting ? TimeSpan.FromSeconds(15) : TimeSpan.Zero))
        {
            Shutdown();
            return;
        }

        _startedUtc = DateTime.UtcNow;
        Config = AppConfig.Load("Client");
        L.SetLanguage(Config.Language);
        ClientLog.Info($"Cliente {AppInfo.Version} iniciado; equipo {Config.StationId}.");

        // El cliente controla la energía del equipo: evita la suspensión, la
        // hibernación y el apagado automático de la pantalla mientras se ejecuta.
        PowerManagementService.Enable();
        _keepAwakeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _keepAwakeTimer.Tick += (_, _) => PowerManagementService.Enable();
        _keepAwakeTimer.Start();

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

    /// <summary>
    /// Toma el mutex de instancia única. Devuelve false si ya hay un cliente
    /// ejecutándose en esta sesión de Windows.
    /// </summary>
    /// <param name="wait">
    /// Tiempo máximo a esperar a que el proceso anterior libere el mutex. Solo se usa
    /// tras una actualización, cuando el proceso viejo todavía está cerrando.
    /// </param>
    private bool AcquireSingleInstance(TimeSpan? wait = null)
    {
        var name = $"Local\\CyberManagerClient-{Environment.MachineName}";
        var deadline = DateTime.UtcNow + (wait ?? TimeSpan.Zero);

        while (true)
        {
            try
            {
                _instanceMutex = new Mutex(true, name, out var createdNew);
                if (createdNew) return true;

                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
            catch
            {
                // Si no se puede crear el mutex, se permite arrancar igualmente.
                return true;
            }

            if (DateTime.UtcNow >= deadline) return false;
            Thread.Sleep(500);
        }
    }

    private void ReleaseSingleInstance()
    {
        try { _instanceMutex?.ReleaseMutex(); } catch { /* no era el dueño */ }
        try { _instanceMutex?.Dispose(); } catch { /* cerrando */ }
        _instanceMutex = null;
    }

    /// <summary>
    /// Registra los errores no controlados en un archivo del equipo y avisa en
    /// pantalla. Sin esto, cualquier excepción cerraba el cliente sin rastro.
    /// </summary>
    private static void InstallCrashHandlers()
    {
        Current.DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("UI", args.Exception);
            args.Handled = true;
            try
            {
                MessageBox.Show(
                    "Ocurrió un error inesperado en el cliente. El equipo sigue bloqueado.\n" +
                    "Si el problema se repite, avise al administrador.",
                    "CyberManager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { /* sin interfaz disponible */ }
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args)
            => LogCrash("AppDomain", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash("Task", args.Exception);
            args.SetObserved();
        };
    }

    private static void LogCrash(string source, Exception? error)
    {
        try
        {
            var file = Path.Combine(AppPaths.DataDir("Client"), "client-errors.log");
            var info = new FileInfo(file);
            if (info.Exists && info.Length > 1_000_000)
            {
                try { File.Delete(file); } catch { /* se sigue usando */ }
            }

            File.AppendAllText(file,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {error}{Environment.NewLine}");
        }
        catch
        {
            // Un fallo al registrar no debe provocar otro fallo.
        }
    }

    private async Task StartAsync()
    {
        await StopAsync();

        // El token es la credencial principal; la IP/MAC se mantienen como respaldo
        // para equipos instalados antes de que el servidor asignara tokens.
        Api = CreateStationApi();
        Realtime = CreateRealtime();

        try
        {
            // Conectar usando el método apropiado según la configuración
            await ConnectRealtimeAsync();
            _lastConnectAttempt = DateTime.UtcNow;
            ClientLog.Info("Conectado al servidor en tiempo real.");
        }
        catch (Exception ex)
        {
            // El servidor puede estar apagado al arrancar; se reintenta en cada refresco.
            ClientLog.Error("No se pudo conectar al servidor en tiempo real", ex);
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

        // La actualización ya se aplicó y el proceso nuevo arrancó: se quita la
        // marca para que no se reinicie en bucle.
        ClientUpdateInstaller.TryDelete(Path.Combine(AppContext.BaseDirectory,
            ClientUpdateInstaller.PendingFile));
    }

    /// <summary>
    /// Cliente de la API de la estación. El token es la credencial principal; la
    /// IP/MAC se mantienen como respaldo para equipos instalados antes de que el
    /// servidor asignara tokens.
    /// </summary>
    private StationApi CreateStationApi()
        => CreateStationApi(Config.ServerUrl);

    private StationApi CreateStationApi(string url, TimeSpan? timeout = null)
        => string.IsNullOrWhiteSpace(Config.StationToken)
            ? new StationApi(url, Config.StationId,
                Config.StationIpAddress ?? string.Empty, Config.StationMacAddress ?? string.Empty,
                null, timeout ?? TimeSpan.FromSeconds(15))
            : new StationApi(url, Config.StationId, Config.StationToken,
                timeout ?? TimeSpan.FromSeconds(15));

    private StationRealtime CreateRealtime()
    {
        var realtime = new StationRealtime();
        realtime.CommandReceived += OnCommandReceived;
        realtime.StationStateChanged += OnStationStateChanged;
        realtime.SessionWarning += OnSessionWarning;
        realtime.WallpaperChanged += OnWallpaperChanged;
        realtime.PromoVideosChanged += OnPromoVideosChanged;
        realtime.ScreenFrameRequested += OnScreenFrameRequested;
        realtime.RemoteInputReceived += OnRemoteInputReceived;
        realtime.ClientUpdateAvailable += OnClientUpdateAvailable;
        realtime.ConnectionChanged += OnRealtimeConnectionChanged;
        return realtime;
    }

    private Task ConnectRealtimeAsync()
    {
        if (Api is null || Realtime is null) return Task.CompletedTask;

        return string.IsNullOrWhiteSpace(Config.StationToken)
            ? Realtime.ConnectAsync(Api.BaseUrl, Config.StationId,
                Config.StationIpAddress ?? string.Empty, Config.StationMacAddress ?? string.Empty)
            : Realtime.ConnectAsync(Api.BaseUrl, Config.StationId, Config.StationToken);
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

    /// <summary>
    /// Relanza el cliente para cargar los binarios nuevos. Lanza un relanzador que
    /// espera a que este proceso termine; si no se puede lanzar, no se cierra nada
    /// y el equipo sigue bloqueado.
    /// </summary>
    private void RestartForUpdate()
    {
        if (_restarting) return;
        _restarting = true;

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            _restarting = false;
            return;
        }

        try
        {
            // Se libera el mutex para que el proceso nuevo pueda arrancar.
            ReleaseSingleInstance();

            var start = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
                Arguments = $"/c ping -n 6 127.0.0.1 >nul & start \"\" \"{exe}\""
            };

            if (Process.Start(start) is null)
            {
                // No se pudo lanzar: se sigue funcionando con la versión actual y
                // se reintenta en el próximo refresco.
                _restarting = false;
                AcquireSingleInstance();
                return;
            }

            Shutdown();
        }
        catch
        {
            _restarting = false;
            AcquireSingleInstance();
        }
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
            Realtime.ConnectionChanged -= OnRealtimeConnectionChanged;
            await Realtime.DisposeAsync();
            Realtime = null;
        }

        // Se libera el HttpClient del equipo para no dejar conexiones abiertas
        // cuando la estación se reconfigura o se cierra.
        Api?.Dispose();
        Api = null;

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
        // Se espera unos segundos tras arrancar: el proceso nuevo borra la marca al
        // terminar de iniciar y así no se reinicia en bucle.
        if (!_restarting && DateTime.UtcNow - _startedUtc > TimeSpan.FromSeconds(20) &&
            File.Exists(Path.Combine(AppContext.BaseDirectory, ClientUpdateInstaller.PendingFile)))
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

            // La dirección guardada dejó de responder: puede que el servidor tenga
            // otra IP. Se busca en la red y, si aparece, se reconecta sin reiniciar.
            await TryRelocateServerAsync();
        }

        // El latido se envía aunque falle la consulta de estado, para que el
        // servidor no marque el equipo como desconectado teniendo el hub activo.
        await SendHeartbeatAsync();

        if (Realtime is not null && !Realtime.IsConnected &&
            DateTime.UtcNow - _lastConnectAttempt > TimeSpan.FromSeconds(20))
        {
            _lastConnectAttempt = DateTime.UtcNow;
            try
            {
                await ConnectRealtimeAsync();
            }
            catch { /* se reintenta luego */ }
        }
    }

    /// <summary>
    /// Busca el servidor en la red local cuando la dirección guardada no responde.
    /// Se limita a un intento cada 30 segundos para no saturar la red mientras el
    /// servidor está apagado.
    /// </summary>
    private async Task TryRelocateServerAsync()
    {
        if (_locating || _shuttingDown || Api is null) return;
        if (DateTime.UtcNow - _lastLocateAttempt < TimeSpan.FromSeconds(30)) return;

        _lastLocateAttempt = DateTime.UtcNow;
        _locating = true;
        try
        {
            var found = await ServerLocator.FindWorkingAsync(Config.ServerUrl);
            if (found is null) return;

            // Sigue respondiendo la dirección actual: no hay nada que cambiar.
            if (string.Equals(ServerLocator.Normalize(found),
                    ServerLocator.Normalize(Api.BaseUrl), StringComparison.OrdinalIgnoreCase))
                return;

            // Se comprueba con el token del equipo: así no se acepta cualquier
            // servidor de la red que responda /api/health.
            if (!await ValidateStationAsync(found)) return;

            await SwitchServerAsync(found);
        }
        catch
        {
            // Sin red disponible: se reintenta en el próximo refresco.
        }
        finally
        {
            _locating = false;
        }
    }

    /// <summary>
    /// Comprueba que el servidor encontrado conoce este equipo: pide el estado con
    /// el token de la estación. Un servidor ajeno (o con otro token) se descarta.
    /// </summary>
    private async Task<bool> ValidateStationAsync(string url)
    {
        StationApi? probe = null;
        try
        {
            probe = CreateStationApi(url, TimeSpan.FromSeconds(8));
            await probe.StateAsync();
            return true;
        }
        catch (Exception ex)
        {
            ClientLog.Error($"El servidor {url} no reconoce este equipo", ex);
            return false;
        }
        finally
        {
            probe?.Dispose();
        }
    }

    /// <summary>
    /// Cambia la dirección del servidor en caliente: guarda la nueva URL, reconstruye
    /// la API y el canal en tiempo real y refresca el estado. No cierra la pantalla
    /// de bloqueo ni la barra, así que el equipo no parpadea.
    /// </summary>
    private async Task SwitchServerAsync(string url)
    {
        url = ServerLocator.Normalize(url);
        var previous = Config.ServerUrl;
        ClientLog.Info($"Servidor encontrado en {url} (antes {previous}). Se reconecta.");

        Config.ServerUrl = url;
        try { Config.Save("Client"); } catch { /* carpeta de solo lectura */ }

        Api?.Dispose();
        Api = CreateStationApi();

        var old = Realtime;
        Realtime = null;
        if (old is not null)
        {
            try { await old.DisposeAsync(); } catch { /* ya cerrado */ }
        }

        Realtime = CreateRealtime();
        _lastConnectAttempt = DateTime.UtcNow;
        try { await ConnectRealtimeAsync(); } catch { /* el refresco reintenta */ }

        await RefreshAsync();
    }

    private Task SendHeartbeatAsync()
    {
        if (Realtime is null || !Realtime.IsConnected) return Task.CompletedTask;

        var heartbeat = new AgentHeartbeat(
            Config.StationId,
            Environment.MachineName,
            string.Empty,
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

        // El servidor es la fuente de verdad del bloqueo. Si dice que el equipo está
        // desbloqueado (por ejemplo tras recargar o extender tiempo), se limpia el
        // bloqueo forzado local aunque el comando Unlock se haya perdido.
        if (!state.Locked) _forcedLock = false;

        SetLocked(state.Locked || _forcedLock || !sessionActive, state);
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

        // Los atajos del sistema solo se bloquean mientras la pantalla de bloqueo
        // está visible, no durante la sesión pagada.
        _lockWindow.SetKeyboardBlocked(true);

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
            _lockWindow.SetKeyboardBlocked(false);
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
    {
        ClientLog.Info($"Orden recibida: {command.Command} {command.Argument}".TrimEnd());
        Dispatcher.BeginInvoke(() => HandleCommand(command));
    }

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
            // Se recrea para reiniciar el tiempo máximo: si el administrador vuelve
            // a enviar la orden, el bloqueo sigue vigente otros 10 minutos.
            _inputBlocker?.Dispose();
            _inputBlocker = new InputBlocker();
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

        // Nunca se aceptan rutas ni procesos críticos: una orden mal enviada no
        // puede tumbar Windows ni el propio CyberManager.
        if (bare.Contains('\\') || bare.Contains('/') || ProtectedProcesses.Contains(bare))
            return;

        foreach (var process in Process.GetProcessesByName(bare))
        {
            try { process.Kill(true); } catch { /* proceso protegido o ya cerrado */ }
        }
    }

    private void OnStationStateChanged(StationStateEvent state)
        => Dispatcher.BeginInvoke(async () => await RefreshAsync());

    /// <summary>
    /// Si se pierde la conexión, se libera el bloqueo de entrada para que el equipo
    /// no quede inutilizable esperando una orden que ya no puede llegar.
    /// </summary>
    private void OnRealtimeConnectionChanged(bool connected)
    {
        ClientLog.Info(connected ? "Conexión en tiempo real restablecida." : "Conexión en tiempo real perdida.");
        if (connected) return;
        Dispatcher.BeginInvoke(() => SetInputBlocked(false));
    }

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
            if (login.ShowDialog() != true) return;
        }
        finally
        {
            _lockWindow.SuppressActivation = false;
        }

        // Sin conexión no hay configuración remota: el administrador desbloquea el
        // equipo con la credencial guardada. Al volver el servidor, su estado manda.
        if (login.Offline || login.Api is null)
        {
            ClientLog.Info("Desbloqueo local de administrador sin conexión al servidor.");
            _forcedLock = false;
            SetLocked(false, State);
            MessageBox.Show(
                "No hay conexión con el servidor. El equipo quedó desbloqueado con la " +
                "credencial de administrador guardada en este equipo.\n\n" +
                "Cuando vuelva la conexión, el servidor retomará el control de la estación.",
                "CyberManager", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshAsync();
            return;
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
        _keepAwakeTimer?.Stop();
        PowerManagementService.Disable();
        _wallpapers.Dispose();
        _promos.Dispose();
        ReleaseSingleInstance();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _shuttingDown = true;
        base.OnSessionEnding(e);
    }
}