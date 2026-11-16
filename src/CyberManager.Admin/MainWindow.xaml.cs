using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Admin.Services;
using CyberManager.Admin.Views;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin;

public partial class MainWindow : Window
{
    private const int MaxLogEntries = 250;

    /// <summary>Vistas reservadas a administradores y técnicos.</summary>
    private static readonly HashSet<string> AdministrativeViews = new(StringComparer.Ordinal)
    {
        "products", "inventory", "purchases", "stations", "wallpapers", "promos", "reports", "settings"
    };

    private readonly Dictionary<string, UserControl> _views = new();
    private readonly DispatcherTimer _headerTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly Dictionary<int, string> _stationNames = new();
    private readonly Dictionary<int, StationStatus> _stationStatus = new();

    private string _current = "dashboard";
    private bool? _connected;
    private bool _switchingUser;
    private AppLanguage _language = L.Language;
    private List<SharedNoteDto> _sharedNotes = new();
    private readonly List<LogEntry> _logEntries = new();
    private readonly string _placementKey;

    private sealed record LogEntry(DateTime Time, string Title, string Message);

    public MainWindow()
    {
        InitializeComponent();

        // Cada usuario de la consola conserva su propia posición y tamaño.
        _placementKey = "main:" + App.Session.Username;

        UserText.Text = App.Session.FullName;
        SessionNameText.Text = App.Session.FullName;
        VersionText.Text = $"CyberManager {AppInfo.Version}";
        UpdateWindowTitle();
        UpdateRoleText();
        ApplyRoleVisibility();

        App.NavigationRequested += OnNavigationRequested;
        App.BusinessIdentityChanged += OnBusinessIdentityChanged;
        AppearanceService.Changed += ApplyAppearance;
        ApplyAppearance(AppearanceService.Current);
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        _headerTimer.Tick += async (_, _) => await RefreshHeaderAsync();
    }

    private void UpdateRoleText()
    {
        RoleText.Text = Labels.RoleName(App.Session.Role);
        SessionRoleText.Text = RoleText.Text;
    }

    private static bool IsCashier => App.Session.Role == UserRole.Cashier;

    /// <summary>
    /// El cajero solo ve las funciones de operación: tablero, punto de venta, caja,
    /// clientes y reservas. Las vistas administrativas se ocultan y se bloquean.
    /// </summary>
    private void ApplyRoleVisibility()
    {
        if (!IsCashier) return;

        foreach (var button in NavPanel.Children.OfType<Button>())
        {
            if (button.CommandParameter is string key && AdministrativeViews.Contains(key))
                button.Visibility = Visibility.Collapsed;
        }

        CatalogLabel.Visibility = Visibility.Collapsed;
        SystemLabel.Visibility = Visibility.Collapsed;

        // El registro de terminales es información administrativa. Las notas
        // compartidas siguen visibles para todos los usuarios.
        LogSection.Visibility = Visibility.Collapsed;
        LogSectionSplitter.Visibility = Visibility.Collapsed;
        LogRow.Height = new GridLength(0);
        LogRow.MinHeight = 0;
        LogSplitRow.Height = new GridLength(0);
        NotesRow.Height = new GridLength(1, GridUnitType.Star);
        NotesRow.MinHeight = 0;
    }

    /// <summary>Muestra el nombre y la versión del software en la barra de título de Windows.</summary>
    private void UpdateWindowTitle()
        => Title = $"{L.T("CyberManager · Administración")} · v{AppInfo.Version}";

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsShuttingDown && !App.IsSessionEnding && !_switchingUser)
        {
            var message = L.T("¿Cerrar la consola de administración?") + Environment.NewLine +
                          Environment.NewLine +
                          L.T("El servidor seguirá ejecutándose en segundo plano.");
            var result = MessageBox.Show(this, message, L.T("Cerrar CyberManager"),
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Barra de título nativa de Windows: los iconos de minimizar/cerrar siempre son visibles.
        NativeWindow.ApplyDarkTitleBar(this);
        NativeWindow.RestorePlacement(this, WindowPlacementStore.Load(_placementKey));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(() => NativeWindow.EnsureVisible(this));
    }

    /// <summary>Ajusta el tamaño de los iconos y textos del menú lateral.</summary>
    private void ApplyAppearance(AppearanceSettings settings)
    {
        var languageChanged = _language != L.Language;
        _language = L.Language;

        foreach (var button in NavPanel.Children.OfType<Button>())
        {
            button.FontSize = settings.NavFontSize;
            if (button.Content is not StackPanel panel) continue;

            if (panel.Children.Count > 0 && panel.Children[0] is TextBlock icon)
            {
                icon.FontSize = settings.NavIconSize;
                icon.Margin = new Thickness(0, 0, 10 + Math.Max(0, settings.NavIconSize - 16) / 2, 0);
            }

            if (panel.Children.Count > 1 && panel.Children[1] is TextBlock label)
                label.FontSize = settings.NavFontSize;
        }

        if (!languageChanged) return;

        // Al cambiar el idioma se traduce la ventana y se reconstruyen las vistas
        // para que los textos generados por código también queden traducidos.
        Loc.Apply(this);
        UpdateWindowTitle();
        UpdateRoleText();
        ConnectionText.Text = L.T(ConnectionText.Text);
        _views.Clear();
        Navigate(_current);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NativeWindow.EnsureVisible(this);
        Loc.Apply(this);
        UpdateRoleText();
        AddLog(L.T("Consola"), "Sesión iniciada en la consola de administración.", "#22D3EE");

        await App.InitializeRealtimeAsync();
        await RefreshHeaderAsync();
        await RefreshLogoAsync();
        await RefreshNotesAsync();
        Navigate("dashboard");
        _headerTimer.Start();

        // La consulta de actualizaciones se hace una sola vez, al abrir la consola.
        _ = CheckForUpdatesOnStartupAsync();

        if (App.Realtime is not null)
        {
            App.Realtime.ConnectionChanged += OnConnectionChanged;
            App.Realtime.EventReceived += OnRealtimeEvent;
            App.Realtime.StationStateChanged += OnRealtimeStationState;
            OnConnectionChanged(App.Realtime.IsConnected);
        }
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        WindowPlacementStore.SaveWindow(_placementKey, this);
        App.NavigationRequested -= OnNavigationRequested;
        App.BusinessIdentityChanged -= OnBusinessIdentityChanged;
        AppearanceService.Changed -= ApplyAppearance;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _headerTimer.Stop();

        if (App.Realtime is not null)
        {
            App.Realtime.ConnectionChanged -= OnConnectionChanged;
            App.Realtime.EventReceived -= OnRealtimeEvent;
            App.Realtime.StationStateChanged -= OnRealtimeStationState;
        }

        // Al cambiar de usuario la conexión ya se cerró y la ventana nueva la
        // vuelve a abrir; la aplicación no debe terminar.
        if (_switchingUser) return;

        await App.DisposeRealtimeAsync();
        App.ShutdownApplication();
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea) && WindowState == WindowState.Normal)
            Dispatcher.BeginInvoke(() => NativeWindow.EnsureVisible(this));
    }

    private void OnBusinessIdentityChanged()
        => Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = RefreshHeaderAsync();
            _ = RefreshLogoAsync();
        }));

    private void OnNavigationRequested(string key) => Dispatcher.Invoke(() => Navigate(key));

    private void OnConnectionChanged(bool connected) => Dispatcher.Invoke(() =>
    {
        ConnectionDot.Fill = (Brush)FindResource(connected ? "SuccessBrush" : "WarningBrush");
        ConnectionText.Text = connected ? L.T("En línea · tiempo real") : L.T("Reconectando...");

        if (_connected == connected) return;
        _connected = connected;

        AddLog("Conexión",
            connected ? "Servidor en línea · tiempo real activo." : "Conexión perdida. Reconectando...",
            connected ? "#22C55E" : "#F59E0B");
    });

    private void Navigate(string key)
    {
        if (IsCashier && AdministrativeViews.Contains(key))
            key = "dashboard";

        if (!_views.TryGetValue(key, out var view))
        {
            view = key switch
            {
                "pos" => new PosView(),
                "cash" => new CashView(),
                "products" => new ProductsView(),
                "inventory" => new InventoryView(),
                "purchases" => new PurchasesView(),
                "stations" => new StationsView(),
                "customers" => new CustomersView(),
                "reservations" => new ReservationsView(),
                "wallpapers" => new WallpapersView(),
                "promos" => new PromosView(),
                "reports" => new ReportsView(),
                "settings" => new SettingsView(),
                _ => new DashboardView()
            };
            _views[key] = view;
        }

        _current = key;
        ContentHost.Content = view;

        foreach (var button in NavPanel.Children.OfType<Button>())
            button.Tag = string.Equals(button.CommandParameter as string, key, StringComparison.Ordinal) ? "active" : null;

        if (view is IRefreshable refreshable)
            _ = Ui.Run(refreshable.RefreshAsync, "No se pudieron cargar los datos");
    }

    public void RefreshCurrent()
    {
        if (_views.TryGetValue(_current, out var view) && view is IRefreshable refreshable)
            _ = Ui.Run(refreshable.RefreshAsync, "No se pudieron actualizar los datos");
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string key }) Navigate(key);
    }

    // ===================== Sesión y cambio de cajero =====================

    /// <summary>
    /// Abre el menú de sesión del panel izquierdo: muestra el usuario actual,
    /// permite cambiar de usuario (con contraseña) y salir de la consola.
    /// </summary>
    private async void SessionMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = await BuildSessionMenuAsync();
        menu.PlacementTarget = SessionButton;
        menu.Placement = PlacementMode.Top;
        menu.IsOpen = true;
    }

    /// <summary>Construye el menú de sesión con el usuario actual y las opciones.</summary>
    private async Task<ContextMenu> BuildSessionMenuAsync()
    {
        var menu = new ContextMenu
        {
            Background = (Brush)FindResource("SurfaceBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        menu.Items.Add(new MenuItem
        {
            Header = $"{App.Session.FullName} · {Labels.RoleName(App.Session.Role)}",
            IsEnabled = false,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(12, 6, 12, 6),
            Foreground = (Brush)FindResource("MutedBrush"),
            Background = Brushes.Transparent
        });

        menu.Items.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
        menu.Items.Add(SessionMenuItem(L.T("Cambiar de usuario…"), () => _ = SwitchUserAsync(null)));

        // El administrador ve la lista de usuarios activos para cambiar sin
        // escribir el nombre; el cajero cambia con el formulario de acceso.
        if (App.Session.Role == UserRole.Admin)
        {
            var users = await LoadActiveUsersAsync();
            if (users.Count > 0)
            {
                menu.Items.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
                foreach (var user in users)
                {
                    var item = SessionMenuItem(
                        $"{user.FullName} · {Labels.RoleName(user.Role)}",
                        () => _ = SwitchUserAsync(user.Username));
                    menu.Items.Add(item);
                }
            }
        }

        menu.Items.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
        menu.Items.Add(SessionMenuItem(L.T("Salir de CyberManager"), ShutdownFromMenu, danger: true));

        return menu;
    }

    private static MenuItem SessionMenuItem(string header, Action click, bool danger = false)
    {
        var item = new MenuItem
        {
            Header = header,
            Foreground = (Brush)Application.Current.FindResource(danger ? "DangerBrush" : "TextBrush"),
            Background = Brushes.Transparent,
            Padding = new Thickness(12, 7, 12, 7),
            FontSize = 12.5
        };
        item.Click += (_, _) => click();
        return item;
    }

    /// <summary>Usuarios activos que se pueden elegir en el menú de sesión.</summary>
    private static async Task<List<UserDto>> LoadActiveUsersAsync()
    {
        try
        {
            var users = await App.Api.UsersAsync();
            return users
                .Where(u => u.Active &&
                            !string.Equals(u.Username, App.Session.Username,
                                StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => u.FullName)
                .ToList();
        }
        catch
        {
            // Si falla la consulta, queda disponible «Cambiar de usuario…».
            return new List<UserDto>();
        }
    }

    /// <summary>
    /// Pide la contraseña del usuario elegido y, si entra correctamente, vuelve a
    /// abrir la consola con la nueva sesión sin cerrar la aplicación.
    /// </summary>
    private async Task SwitchUserAsync(string? username)
    {
        if (_switchingUser) return;

        var login = new LoginWindow(username) { Owner = this };
        if (login.ShowDialog() != true) return;

        await RestartConsoleAsync();
    }

    /// <summary>
    /// Recrea la ventana principal con la sesión recién iniciada. La aplicación
    /// sigue abierta: solo se cierra la ventana anterior y se abre la nueva.
    /// </summary>
    private async Task RestartConsoleAsync()
    {
        if (_switchingUser) return;
        _switchingUser = true;

        var app = Application.Current;
        var previousMode = app.ShutdownMode;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            // Se cierra el tiempo real de la sesión anterior antes de salir; la
            // ventana nueva lo abre con el token del usuario que acaba de entrar.
            await App.DisposeRealtimeAsync();
            Close();

            var window = new MainWindow();
            app.MainWindow = window;
            window.Show();
        }
        finally
        {
            app.ShutdownMode = previousMode;
            _switchingUser = false;
        }
    }

    private void ShutdownFromMenu()
    {
        var message = L.T("¿Cerrar la consola de administración?") + Environment.NewLine +
                      Environment.NewLine +
                      L.T("El servidor seguirá ejecutándose en segundo plano.");
        if (!Ui.Confirm(message, L.T("Salir de CyberManager"))) return;

        App.ShutdownApplication();
    }

    /// <summary>
    /// Carga el logo del negocio. Se hace aparte del refresco periódico del
    /// encabezado porque la imagen puede pesar varios megabytes.
    /// </summary>
    private async Task RefreshLogoAsync()
    {
        try
        {
            var logo = await App.Api.BusinessLogoAsync();
            var image = Branding.Decode(logo.DataUrl);
            LogoImage.Source = image;
            LogoFrame.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
            LogoFallback.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            // Si el logo no está disponible se conserva la marca de respaldo.
        }
    }

    private async Task RefreshHeaderAsync()
    {
        try
        {
            var rate = await App.Api.TodayRateAsync();
            RateText.Text = rate.Rate > 0 ? $"1 USD = {rate.Rate:N2} Bs" : L.T("Tasa sin definir");

            var settings = await App.Api.SettingsAsync();
            var businessName = settings.TryGetValue("BusinessName", out var business)
                ? business?.Trim()
                : null;

            // El encabezado muestra una sola línea con el nombre del negocio.
            BusinessNameText.Text = string.IsNullOrWhiteSpace(businessName)
                ? "CyberManager"
                : businessName;
            BusinessNameText.ToolTip = L.T("Consola de administración");

            var stations = await App.Api.StationsAsync();
            foreach (var station in stations)
            {
                _stationNames[station.Id] = station.Name;
                _stationStatus[station.Id] = station.Status;
            }

            OnConnectionChanged(App.Realtime?.IsConnected == true);
        }
        catch
        {
            ConnectionDot.Fill = (Brush)FindResource("DangerBrush");
            ConnectionText.Text = L.T("Sin conexión");
        }
    }

    // ===================== Aviso de nueva versión =====================

    /// <summary>
    /// Consulta GitHub una sola vez al abrir la consola y, si ya corresponde el
    /// recordatorio, pregunta si se desea actualizar. «Sí» abre la página de
    /// versiones del proyecto; «No» oculta el aviso para esa versión.
    /// </summary>
    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (IsCashier) return;

        UpdateInfoDto info;
        try
        {
            info = await App.Api.LatestUpdateAsync(refresh: true);
        }
        catch
        {
            // Un fallo de red no debe afectar la apertura de la consola.
            return;
        }

        // Si la consola se cerró mientras se consultaba GitHub, no mostrar el aviso.
        if (!IsVisible) return;

        if (!info.IsReminderDue || string.IsNullOrWhiteSpace(info.LatestVersion)) return;

        var message =
            $"{L.T("Hay una nueva versión disponible:")} {info.LatestVersion}" +
            Environment.NewLine + Environment.NewLine +
            $"{L.T("Versión instalada:")} {info.CurrentVersion}" +
            Environment.NewLine + Environment.NewLine +
            L.T("¿Desea actualizar ahora?");

        var result = MessageBox.Show(this, message, L.T("Actualización disponible"),
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
        {
            OpenReleasesPage();
            return;
        }

        // «No» oculta el aviso de esta versión hasta que se publique otra.
        try { await App.Api.DismissUpdateAsync(info.LatestVersion); }
        catch { /* el aviso se volverá a mostrar en la próxima apertura */ }
    }

    private void OpenReleasesPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppInfo.ReleasesUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message, L.T("Actualización"));
        }
    }

    // ===================== Registro de terminales =====================

    private void OnRealtimeEvent(RealtimeEvent e) => Dispatcher.BeginInvoke(() =>
    {
        var (title, color) = e.Type switch
        {
            "SessionStarted" => ("Sesión iniciada", "#22C55E"),
            "SessionEnded" => ("Sesión cerrada", "#3B82F6"),
            "SessionWarning" => ("Aviso de sesión", "#F59E0B"),
            "Overtime" => ("Tiempo agotado", "#EF4444"),
            "StationOnline" => ("Equipo en línea", "#22C55E"),
            "StationOffline" => ("Equipo sin conexión", "#F59E0B"),
            "AgentOnline" => ("Agente conectado", "#22C55E"),
            "AgentOffline" => ("Agente desconectado", "#F59E0B"),
            "StationMaintenance" => ("Mantenimiento", "#8B5CF6"),
            "CommandSent" => ("Comando enviado", "#22D3EE"),
            "CommandResult" => ("Resultado de comando", "#8FA4C4"),
            "WallpaperChanged" => ("Fondos actualizados", "#8B5CF6"),
            "SharedNoteAdded" => ("Nota agregada", "#22D3EE"),
            "SharedNoteDeleted" => ("Nota eliminada", "#F59E0B"),
            _ => (e.Type, "#8FA4C4")
        };

        AddLog(title, string.IsNullOrWhiteSpace(e.Message) ? e.Type : e.Message!, color);

        if (e.Type is "SharedNoteAdded" or "SharedNoteDeleted") _ = RefreshNotesAsync();
    });

    private void OnRealtimeStationState(StationStateEvent e) => Dispatcher.BeginInvoke(() =>
    {
        if (_stationStatus.TryGetValue(e.StationId, out var previous) && previous == e.Status) return;
        _stationStatus[e.StationId] = e.Status;

        var name = _stationNames.TryGetValue(e.StationId, out var stationName)
            ? stationName
            : $"PC #{e.StationId}";

        var text = Labels.StatusName(e.Status);
        if (e.Status == StationStatus.InUse && e.SessionId is int sessionId)
            text += $" · sesión #{sessionId}";

        AddLog(name, text, Labels.StatusColor(e.Status));
    });

    private void AddLog(string title, string message, string colorHex)
    {
        var accent = ParseColor(colorHex);
        var now = DateTime.Now;

        _logEntries.Add(new LogEntry(now, title, message));
        while (_logEntries.Count > MaxLogEntries)
            _logEntries.RemoveAt(0);

        var texts = new StackPanel();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(accent),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var time = new TextBlock
        {
            Text = now.ToString("HH:mm:ss"),
            FontSize = 10,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(time, 1);
        header.Children.Add(time);

        texts.Children.Add(header);
        texts.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 0)
        });

        // Lista simple: sin tarjeta, solo una línea tenue que separa cada evento.
        LogPanel.Children.Add(new Border
        {
            BorderBrush = (Brush)FindResource("GridLineBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 3),
            Margin = new Thickness(0, 0, 0, 3),
            Child = texts
        });

        while (LogPanel.Children.Count > MaxLogEntries)
            LogPanel.Children.RemoveAt(0);

        var count = LogPanel.Children.Count;
        LogCountText.Text = $"{count} evento{(count == 1 ? "" : "s")}";
        LogScroll.ScrollToEnd();
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0x8F, 0xA4, 0xC4); }
    }

    /// <summary>Guarda el registro visible en un archivo de texto y lo abre con el Bloc de notas.</summary>
    private void OpenLogInNotepad_Click(object sender, RoutedEventArgs e)
    {
        if (_logEntries.Count == 0)
        {
            Ui.Info("El registro está vacío. Todavía no hay eventos para mostrar.", "Registro");
            return;
        }

        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CyberManager", "Admin");
            Directory.CreateDirectory(folder);

            var file = Path.Combine(folder, $"registro-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var lines = new List<string>
            {
                "CyberManager · Registro de actividad",
                $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}",
                new string('-', 70)
            };

            foreach (var entry in _logEntries)
            {
                lines.Add($"{entry.Time:dd/MM/yyyy HH:mm:ss}  {entry.Title}");
                lines.Add(entry.Message);
                lines.Add(string.Empty);
            }

            File.WriteAllLines(file, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{file}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message, "Registro");
        }
    }

    // ===================== Notas =====================

    private async Task RefreshNotesAsync()
    {
        try
        {
            _sharedNotes = await App.Api.SharedNotesAsync();
            RenderNotes();
        }
        catch
        {
            NoteStatusText.Text = L.T("No se pudieron cargar las notas.");
        }
    }

    private void RenderNotes()
    {
        NotesPanel.Children.Clear();

        if (_sharedNotes.Count == 0)
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = L.T("Aún no hay notas. Escriba la primera para compartirla con el equipo."),
                Style = (Style)FindResource("Muted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
            return;
        }

        foreach (var note in _sharedNotes)
            NotesPanel.Children.Add(BuildNoteCard(note));

        // Las notas más recientes se muestran primero.
        NotesScroll.ScrollToTop();
    }

    private Border BuildNoteCard(SharedNoteDto note)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = note.AuthorName,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("AccentBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var time = new TextBlock
        {
            Text = note.CreatedUtc.ToLocalTime().ToString("dd/MM HH:mm"),
            FontSize = 10,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(time, 1);
        header.Children.Add(time);
        texts.Children.Add(header);

        texts.Children.Add(new TextBlock
        {
            Text = note.Text,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 0)
        });
        grid.Children.Add(texts);

        if (App.Session.Role == UserRole.Admin)
        {
            var delete = new Button
            {
                Content = "✕",
                Style = (Style)FindResource("IconButton"),
                FontSize = 10,
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Eliminar esta nota para todos los usuarios."
            };
            ToolTipService.SetInitialShowDelay(delete, 2000);
            delete.Click += (_, _) => DeleteNote(note);
            Grid.SetColumn(delete, 1);
            grid.Children.Add(delete);
        }

        // Lista simple: sin tarjeta, solo una línea tenue que separa cada nota.
        return new Border
        {
            BorderBrush = (Brush)FindResource("GridLineBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 3),
            Margin = new Thickness(0, 0, 0, 3),
            Child = grid
        };
    }

    private async void PublishNote_Click(object sender, RoutedEventArgs e)
    {
        var text = NoteInput.Text.Trim();
        if (text.Length == 0)
        {
            NoteStatusText.Text = L.T("Escriba una nota antes de publicarla.");
            return;
        }

        NoteStatusText.Text = L.T("Publicando...");
        try
        {
            await App.Api.CreateSharedNoteAsync(text);
            NoteInput.Text = string.Empty;
            NoteStatusText.Text = L.T("Nota publicada para todos los usuarios.");
            await RefreshNotesAsync();
        }
        catch (Exception ex)
        {
            NoteStatusText.Text = ex.Message;
        }
    }

    private async void DeleteNote(SharedNoteDto note)
    {
        if (!Ui.Confirm(
                $"¿Eliminar la nota de {note.AuthorName}? Se quitará para todos los usuarios.",
                "Notas"))
            return;

        try
        {
            await App.Api.DeleteSharedNoteAsync(note.Id);
            await RefreshNotesAsync();
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message, "Notas");
        }
    }
}
