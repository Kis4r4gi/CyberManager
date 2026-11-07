using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Admin.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.Win32;

namespace CyberManager.Admin.Views;

/// <summary>Configuración del negocio, tasa de cambio, usuarios e instalación.</summary>
public sealed class SettingsView : UserControl, IRefreshable
{
    private readonly Dictionary<string, TextBox> _texts = new();
    private readonly Dictionary<string, CheckBox> _checks = new();
    private readonly Dictionary<string, string> _textDefaults = new();
    private readonly Dictionary<string, bool> _checkDefaults = new();

    private readonly TextBlock _businessStatus = Kit.Muted("Cargue la configuración para comenzar.");
    private readonly TextBlock _rateStatus = Kit.Muted("Consultando tasa...");
    private readonly TextBlock _bcvStatus = Kit.Muted("Actualización automática desde el BCV disponible.");
    private readonly TextBlock _rateToday = new() { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Kit.Brush("AccentBrush") };
    private readonly DatePicker _rateDate = new() { SelectedDate = DateTime.Today };
    private readonly TextBox _rateValue = new();
    private readonly TextBox _rateNotes = new();
    private readonly ComboBox _rateSource = new();
    private readonly DataGrid _rateHistoryGrid;
    private readonly DataGrid _usersGrid;
    private readonly DataGrid _backupsGrid;
    private readonly TextBlock _backupStatus = Kit.Muted("Los respaldos se guardan en el servidor.");
    private readonly TextBlock _backupFolderText = Kit.Muted("Consultando carpeta...");

    private readonly Dictionary<string, TextBox> _appearanceTexts = new();
    private readonly Dictionary<string, Border> _appearanceSwatches = new();
    private readonly TextBlock _appearanceStatus = Kit.Muted("Los cambios se guardan automáticamente en este equipo.");
    private ComboBox _appearancePresetBox = null!;
    private ComboBox _languageBox = null!;
    private Slider _iconSizeSlider = null!;
    private Slider _navFontSlider = null!;
    private Slider _productCardSlider = null!;
    private Slider _stationCardSlider = null!;
    private bool _appearanceLoading;
    private bool _businessLoaded;
    private bool _businessSaving;
    private bool _backupBusy;
    private bool _bcvBusy;

    private readonly DispatcherTimer _businessAutosave = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private readonly DispatcherTimer _appearanceAutosave = new() { Interval = TimeSpan.FromMilliseconds(800) };

    private List<UserDto> _users = new();
    private List<BackupRow> _backups = new();
    private string? _backupFolder;

    public SettingsView()
    {
        _rateHistoryGrid = Kit.Table(
            Kit.Col("Fecha", nameof(RateHistoryRow.Date), 115),
            Kit.Col("Tasa Bs/USD", nameof(RateHistoryRow.Rate), 115, "{0:N2}"),
            Kit.Col("Fuente", nameof(RateHistoryRow.Source), 105),
            Kit.Col("Activa", nameof(RateHistoryRow.Active), 85),
            Kit.Col("Registrada", nameof(RateHistoryRow.Created), 145),
            Kit.Col("Notas", nameof(RateHistoryRow.Notes)));

        _usersGrid = Kit.Table(
            Kit.Col("Usuario", nameof(UserRow.Username), 150),
            Kit.Col("Nombre completo", nameof(UserRow.FullName)),
            Kit.Col("Rol", nameof(UserRow.Role), 140),
            Kit.Col("Estado", nameof(UserRow.Status), 105),
            Kit.Col("Último acceso", nameof(UserRow.LastLogin), 160));

        _backupsGrid = Kit.Table(
            Kit.Col("Fecha", nameof(BackupRow.Date), 165),
            Kit.Col("Tipo", nameof(BackupRow.Kind), 130),
            Kit.Col("Tamaño", nameof(BackupRow.Size), 100),
            Kit.Col("Archivo", nameof(BackupRow.FileName)));

        var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        tabs.Items.Add(new TabItem { Header = "Negocio y facturación", Content = BuildBusinessTab() });
        tabs.Items.Add(new TabItem { Header = "Tasa de cambio", Content = BuildRateTab() });
        tabs.Items.Add(new TabItem { Header = "Usuarios", Content = BuildUsersTab() });
        tabs.Items.Add(new TabItem { Header = "Apariencia", Content = BuildAppearanceTab() });
        tabs.Items.Add(new TabItem { Header = "Respaldos", Content = BuildBackupTab() });
        tabs.Items.Add(new TabItem { Header = "Instalación", Content = BuildInstallTab() });

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(Kit.PageHeader("Configuración",
            "Datos fiscales, tasa del día, usuarios, apariencia y respaldos de la base de datos.",
            Kit.Button("Recargar", "GhostButton", () => _ = Ui.Run(RefreshAsync, "No se pudo cargar la configuración"))));
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        Content = root;

        _businessAutosave.Tick += async (_, _) =>
        {
            _businessAutosave.Stop();
            await SaveBusinessAsync();
        };
        _appearanceAutosave.Tick += (_, _) =>
        {
            _appearanceAutosave.Stop();
            ApplyAppearance(previewOnly: false);
        };
        Unloaded += (_, _) =>
        {
            _businessAutosave.Stop();
            _appearanceAutosave.Stop();
        };

        _usersGrid.MouseDoubleClick += (_, _) => EditUser();
        _rateSource.ItemsSource = new List<Option<RateSource>>
        {
            new(RateSource.Manual, "Manual"),
            new(RateSource.Bcv, "BCV"),
            new(RateSource.Paralelo, "Paralelo")
        };
        _rateSource.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _rateSource.SelectedIndex = 0;

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudo cargar la configuración");
    }

    // ============================================================
    //  Negocio
    // ============================================================
    private FrameworkElement BuildBusinessTab()
    {
        var panel = new StackPanel();

        panel.Children.Add(Kit.Section("Datos del negocio"));
        panel.Children.Add(Description("Estos datos aparecen en la factura y en los reportes impresos."));

        var identity = new UniformGrid { Columns = 2 };
        AddText(identity, "BusinessName", "Nombre o razón social", "Cyber Café");
        AddText(identity, "BusinessTaxId", "RIF", "J-00000000-0");
        AddText(identity, "BusinessPhone", "Teléfono", "");
        AddText(identity, "BusinessAddress", "Dirección fiscal", "");
        panel.Children.Add(identity);

        panel.Children.Add(Kit.Section("Facturación"));
        var billing = new UniformGrid { Columns = 2 };
        AddText(billing, "CurrencySymbol", "Símbolo de moneda base", "$");
        AddText(billing, "LocalCurrencySymbol", "Símbolo de moneda local", "Bs");
        AddText(billing, "DefaultTaxRate", "IVA por defecto (%)", "16");
        AddText(billing, "IgtfRate", "IGTF (%)", "3");
        AddText(billing, "InvoicePrefix", "Prefijo de factura", "F");
        AddText(billing, "ControlNumberStart", "Número de control inicial", "1");
        AddText(billing, "WarningMinutes", "Aviso antes de terminar (min)", "5");
        AddText(billing, "GraceMinutes", "Tiempo de gracia (min)", "5");
        panel.Children.Add(billing);

        var checks = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 8) };
        AddCheck(checks, "IgtfEnabled", "Cobrar IGTF en pagos con divisas", true);
        AddCheck(checks, "AllowPostpaid", "Permitir sesiones postpago", true);
        AddCheck(checks, "AutoLockOvertime", "Bloquear equipo al agotarse el tiempo", true);
        AddCheck(checks, "KioskMode", "Modo kiosco en los equipos", true);
        panel.Children.Add(checks);

        panel.Children.Add(Kit.Section("Ticket y operación"));
        var operation = new UniformGrid { Columns = 2 };
        AddText(operation, "PrinterName", "Impresora de tickets (opcional)", "");
        AddText(operation, "TicketCopies", "Copias por ticket (1 a 5)", "1");
        panel.Children.Add(operation);

        AddText(panel, "TicketHeader", "Encabezado del ticket (opcional)",
            "", fullWidth: true);

        var ticketChecks = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 8) };
        AddCheck(ticketChecks, "AutoPrintTicket", "Imprimir ticket automáticamente al cobrar", false);
        AddCheck(ticketChecks, "ScannerAutoAdd", "El lector de códigos agrega al presionar Enter", true);
        panel.Children.Add(ticketChecks);

        AddText(panel, "TicketFooter", "Pie de página del ticket",
            "Gracias por su visita. ¡Vuelva pronto!", multiline: true, fullWidth: true);

        var save = Kit.Button("Guardar ahora", "SuccessButton", () => _ = SaveBusinessAsync());
        save.Padding = new Thickness(16, 11, 16, 11);
        save.Margin = new Thickness(0, 8, 0, 0);
        save.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(save);

        panel.Children.Add(Description(
            "Los cambios se guardan automáticamente unos instantes después de editarlos. " +
            "El botón «Guardar ahora» fuerza el guardado inmediato."));

        _businessStatus.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(_businessStatus);

        return Scroll(panel);
    }

    private async Task SaveBusinessAsync()
    {
        if (!_businessLoaded) return;
        if (_businessSaving)
        {
            ScheduleBusinessAutosave();
            return;
        }

        var values = new Dictionary<string, string>();

        foreach (var (key, box) in _texts)
            values[key] = Normalize(box.Text);

        foreach (var (key, check) in _checks)
            values[key] = (check.IsChecked == true).ToString().ToLowerInvariant();

        _businessSaving = true;
        _businessStatus.Text = "Guardando...";
        _businessStatus.Foreground = Kit.Brush("MutedBrush");

        try
        {
            await App.Api.SaveSettingsAsync(values);
            _businessStatus.Text = $"Configuración guardada automáticamente a las {DateTime.Now:HH:mm:ss}.";
            _businessStatus.Foreground = Kit.Brush("SuccessBrush");
        }
        catch (Exception ex)
        {
            _businessStatus.Text = ex.Message;
            _businessStatus.Foreground = Kit.Brush("DangerBrush");
        }
        finally
        {
            _businessSaving = false;
        }
    }

    private void ScheduleBusinessAutosave()
    {
        if (!_businessLoaded) return;
        _businessAutosave.Stop();
        _businessAutosave.Start();
    }

    private static string Normalize(string text)
    {
        text = text.Trim();
        if (TryParseDecimal(text, out var value))
            return value.ToString(CultureInfo.InvariantCulture);
        return text;
    }

    // ============================================================
    //  Tasa de cambio
    // ============================================================
    private FrameworkElement BuildRateTab()
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var form = new StackPanel();
        form.Children.Add(Kit.Section("Tasa del día"));
        form.Children.Add(Description(
            "La tasa se guarda como una foto del día: cada venta conserva la tasa con la que se cobró. " +
            "Los montos contables siempre se llevan en USD."));
        form.Children.Add(_rateToday);
        form.Children.Add(_rateStatus);
        form.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 10) });

        form.Children.Add(Kit.Field("Fecha", _rateDate));
        _rateValue.ToolTip = "Bs por 1 USD";
        form.Children.Add(Kit.Field("Tasa (Bs por USD)", _rateValue));
        form.Children.Add(Kit.Field("Fuente", _rateSource));
        form.Children.Add(Kit.Field("Notas", _rateNotes));

        var save = Kit.Button("Registrar tasa", "PrimaryButton", SaveRateAsync);
        save.Padding = new Thickness(16, 11, 16, 11);
        form.Children.Add(save);

        form.Children.Add(Description(
            "Al guardar una tasa para una fecha, las demás fuentes de esa fecha se desactivan automáticamente."));

        form.Children.Add(Kit.Section("Tasa oficial del BCV"));
        form.Children.Add(Description(
            "El servidor consulta el sitio del Banco Central de Venezuela cada 30 minutos y registra " +
            "la tasa oficial del dólar como la tasa del día. La fecha valor publicada por el BCV " +
            "queda anotada para fines de auditoría."));

        var bcv = Kit.Button("Consultar BCV ahora", "GhostButton", () => _ = ConsultarBcvAsync());
        bcv.HorizontalAlignment = HorizontalAlignment.Left;
        bcv.Margin = new Thickness(0, 0, 0, 8);
        form.Children.Add(bcv);

        var bcvChecks = new UniformGrid { Columns = 1 };
        AddCheck(bcvChecks, "BcvAutoUpdate", "Actualizar automáticamente desde el BCV", true);
        form.Children.Add(bcvChecks);

        _bcvStatus.Margin = new Thickness(0, 6, 0, 0);
        _bcvStatus.TextWrapping = TextWrapping.Wrap;
        form.Children.Add(_bcvStatus);

        var formCard = Kit.Card(form, 16);
        formCard.Margin = new Thickness(0, 0, 14, 0);
        formCard.VerticalAlignment = VerticalAlignment.Top;
        content.Children.Add(formCard);

        var history = new Grid();
        history.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        history.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        history.Children.Add(Kit.Section("Historial (últimos 30 días)"));
        Grid.SetRow(_rateHistoryGrid, 1);
        history.Children.Add(_rateHistoryGrid);

        var historyCard = Kit.Card(history, 16);
        Grid.SetColumn(historyCard, 1);
        content.Children.Add(historyCard);

        return content;
    }

    private void SaveRateAsync()
    {
        if (_rateDate.SelectedDate is not DateTime date)
        {
            Ui.Error("Seleccione la fecha de la tasa.", "Tasa de cambio");
            return;
        }

        if (!TryParseDecimal(_rateValue.Text, out var rate) || rate <= 0)
        {
            Ui.Error("Indique una tasa mayor que cero.", "Tasa de cambio");
            return;
        }

        var source = (_rateSource.SelectedItem as Option<RateSource>)?.Value ?? RateSource.Manual;
        var notes = string.IsNullOrWhiteSpace(_rateNotes.Text) ? null : _rateNotes.Text.Trim();

        _ = Ui.Run(async () =>
        {
            var result = await App.Api.SetRateAsync(new SetExchangeRateRequest(date.Date, rate, source, notes));
            _rateStatus.Text = $"Tasa registrada: 1 USD = {result.Rate:N2} Bs ({Labels.RateSourceName(result.Source)}).";
            _rateStatus.Foreground = Kit.Brush("SuccessBrush");
            await LoadRatesAsync();
        }, "No se pudo registrar la tasa");
    }

    private async Task ConsultarBcvAsync()
    {
        if (_bcvBusy) return;
        _bcvBusy = true;
        _bcvStatus.Text = "Consultando la página del BCV...";
        _bcvStatus.Foreground = Kit.Brush("MutedBrush");

        try
        {
            var result = await App.Api.FetchBcvRateAsync();
            _rateValue.Text = result.Rate.ToString("N2", CultureInfo.CurrentCulture);
            _rateDate.SelectedDate = result.Date;
            _bcvStatus.Text = result.Updated
                ? $"Tasa BCV registrada: 1 USD = {result.Rate:N2} Bs · fecha valor {result.ValueDate:dd/MM/yyyy}."
                : $"La tasa del BCV no cambió: 1 USD = {result.Rate:N2} Bs · fecha valor {result.ValueDate:dd/MM/yyyy}.";
            _bcvStatus.Foreground = Kit.Brush(result.Updated ? "SuccessBrush" : "MutedBrush");
            await LoadRatesAsync();
        }
        catch (Exception ex)
        {
            _bcvStatus.Text = ex.Message;
            _bcvStatus.Foreground = Kit.Brush("DangerBrush");
        }
        finally
        {
            _bcvBusy = false;
        }
    }

    private async Task LoadRatesAsync()
    {
        var today = await App.Api.TodayRateAsync();        _rateToday.Text = today.Rate > 0 ? $"1 USD = {today.Rate:N2} Bs" : "Sin tasa registrada";
        _rateToday.Foreground = today.Rate > 0 ? Kit.Brush("AccentBrush") : Kit.Brush("WarningBrush");
        _rateStatus.Text = today.Rate > 0
            ? $"Fuente: {Labels.RateSourceName(today.Source)} · fecha {today.Date:dd/MM/yyyy}"
            : "Registre una tasa para poder abrir la caja y cobrar.";
        _rateStatus.Foreground = today.Rate > 0 ? Kit.Brush("MutedBrush") : Kit.Brush("WarningBrush");

        if (today.Rate > 0 && string.IsNullOrWhiteSpace(_rateValue.Text))
            _rateValue.Text = today.Rate.ToString("N2", CultureInfo.CurrentCulture);

        var history = await App.Api.RateHistoryAsync();
        _rateHistoryGrid.ItemsSource = history.Select(r => new RateHistoryRow(r)).ToList();
    }

    // ============================================================
    //  Usuarios
    // ============================================================
    private FrameworkElement BuildUsersTab()
    {
        var root = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(Description(
            "Solo los administradores pueden modificar la configuración y gestionar usuarios. " +
            "Las contraseñas deben tener al menos 6 caracteres."));

        var actions = Kit.HStack(8,
            Kit.Button("Nuevo usuario", "PrimaryButton", NewUser),
            Kit.Button("Editar", "GhostButton", EditUser),
            Kit.Button("Desactivar", "DangerButton", DeactivateUser));
        actions.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(actions, 1);
        root.Children.Add(actions);

        Grid.SetRow(_usersGrid, 2);
        root.Children.Add(_usersGrid);

        return root;
    }

    private void NewUser()
    {
        var dialog = new UserDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.CreateRequest is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.CreateUserAsync(dialog.CreateRequest);
            await LoadUsersAsync();
        }, "No se pudo crear el usuario");
    }

    private void EditUser()
    {
        if (_usersGrid.SelectedItem is not UserRow row)
        {
            Ui.Info("Seleccione un usuario de la lista.", "Usuarios");
            return;
        }

        var dialog = new UserDialog(row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.UpdateRequest is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.UpdateUserAsync(row.Source.Id, dialog.UpdateRequest);
            await LoadUsersAsync();
        }, "No se pudo guardar el usuario");
    }

    private void DeactivateUser()
    {
        if (_usersGrid.SelectedItem is not UserRow row)
        {
            Ui.Info("Seleccione un usuario de la lista.", "Usuarios");
            return;
        }

        if (!Ui.Confirm($"¿Desactivar el usuario {row.Username}? No podrá iniciar sesión.",
                "Desactivar usuario"))
            return;

        _ = Ui.Run(async () =>
        {
            await App.Api.DeactivateUserAsync(row.Source.Id);
            await LoadUsersAsync();
        }, "No se pudo desactivar el usuario");
    }

    private async Task LoadUsersAsync()
    {
        _users = await App.Api.UsersAsync();
        _usersGrid.ItemsSource = _users.Select(u => new UserRow(u)).ToList();
    }

    // ============================================================
    //  Apariencia
    // ============================================================
    private FrameworkElement BuildAppearanceTab()
    {
        var panel = new StackPanel();

        panel.Children.Add(Kit.Section("Paleta de colores"));
        panel.Children.Add(Description(
            "Elija un tema predefinido o escriba colores hexadecimales (por ejemplo #22D3EE). " +
            "Los cambios se guardan automáticamente y se aplican a toda la consola de este equipo."));

        // ----- Idioma de la consola: solo español e inglés -----
        _languageBox = new ComboBox
        {
            ItemsSource = new List<Option<string>>
            {
                new("es", "Español"),
                new("en", "English")
            },
            DisplayMemberPath = nameof(Option<object>.DisplayText)
        };
        _languageBox.SelectionChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            ApplyAppearance(previewOnly: false);
        };

        var languageRow = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 12) };
        languageRow.Children.Add(Kit.Field("Idioma de la interfaz", _languageBox));
        languageRow.Children.Add(Kit.Muted("Solo se admiten español e inglés. La consola se actualiza al instante."));
        panel.Children.Add(languageRow);

        var presetRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _appearancePresetBox = new ComboBox { ItemsSource = AppearanceSettings.Presets.ToList(), SelectedIndex = 0 };
        _appearancePresetBox.SelectionChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            if (_appearancePresetBox.SelectedItem is not string name) return;
            var preset = AppearanceSettings.ForPreset(name);
            preset.NavIconSize = _iconSizeSlider.Value;
            preset.NavFontSize = _navFontSlider.Value;
            preset.ProductCardScale = _productCardSlider.Value;
            preset.StationCardScale = _stationCardSlider.Value;
            preset.Language = AppearanceService.Current.Language;
            CopyDashboardPreferences(AppearanceService.Current, preset);
            FillAppearance(preset);
            ApplyAppearance(previewOnly: true);
            ScheduleAppearanceAutosave();
        };
        presetRow.Children.Add(Kit.Field("Tema predefinido", _appearancePresetBox));

        var presetHint = Kit.Muted("Presets: Cyber, Neón, Esmeralda, Ámbar, Océano y Grafito.");
        presetHint.VerticalAlignment = VerticalAlignment.Bottom;
        presetHint.Margin = new Thickness(12, 0, 0, 14);
        Grid.SetColumn(presetHint, 1);
        presetRow.Children.Add(presetHint);
        panel.Children.Add(presetRow);

        var colors = new UniformGrid { Columns = 2 };
        AddAppearanceColor(colors, nameof(AppearanceSettings.Background), "Fondo general");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Surface), "Superficie de tarjetas");
        AddAppearanceColor(colors, nameof(AppearanceSettings.SurfaceAlt), "Superficie alterna");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Border), "Bordes");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Text), "Texto principal");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Muted), "Texto atenuado");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Accent), "Acento principal");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Accent2), "Acento secundario");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Success), "Éxito");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Warning), "Advertencia");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Danger), "Peligro");
        AddAppearanceColor(colors, nameof(AppearanceSettings.Purple), "Morado");
        panel.Children.Add(colors);

        panel.Children.Add(Kit.Section("Menú lateral"));
        var sizes = new UniformGrid { Columns = 2 };
        _iconSizeSlider = MakeSlider(12, 26, 16);
        _iconSizeSlider.ValueChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            ApplyAppearance(previewOnly: true);
            ScheduleAppearanceAutosave();
        };
        sizes.Children.Add(Kit.Field("Tamaño de iconos (12 a 26)", _iconSizeSlider));
        _navFontSlider = MakeSlider(11, 17, 13);
        _navFontSlider.ValueChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            ApplyAppearance(previewOnly: true);
            ScheduleAppearanceAutosave();
        };
        sizes.Children.Add(Kit.Field("Tamaño del texto del menú (11 a 17)", _navFontSlider));
        panel.Children.Add(sizes);

        panel.Children.Add(Kit.Section("Cuadros de artículos y terminales"));
        panel.Children.Add(Description(
            "Ajuste el tamaño de los cuadros del punto de venta y del tablero de terminales. " +
            "Los cambios se aplican al volver a abrir cada vista."));
        var cards = new UniformGrid { Columns = 2 };
        _productCardSlider = MakeSlider(80, 160, 100, 5);
        _productCardSlider.ValueChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            ApplyAppearance(previewOnly: true);
            ScheduleAppearanceAutosave();
        };
        cards.Children.Add(Kit.Field("Cuadros de artículos (80 a 160 %)", _productCardSlider));
        _stationCardSlider = MakeSlider(80, 160, 100, 5);
        _stationCardSlider.ValueChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            ApplyAppearance(previewOnly: true);
            ScheduleAppearanceAutosave();
        };
        cards.Children.Add(Kit.Field("Cuadros de terminales (80 a 160 %)", _stationCardSlider));
        panel.Children.Add(cards);

        var actions = Kit.HStack(8,
            Kit.Button("Aplicar y guardar ahora", "SuccessButton", () => ApplyAppearance(previewOnly: false)),
            Kit.Button("Restablecer", "DangerButton", ResetAppearance));
        actions.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(actions);

        _appearanceStatus.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(_appearanceStatus);

        FillAppearance(AppearanceService.Current);
        return Scroll(panel);
    }

    private static Slider MakeSlider(double min, double max, double value, double tick = 1)
        => new()
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = tick,
            IsSnapToTickEnabled = true,
            TickPlacement = TickPlacement.BottomRight,
            Height = 36
        };

    private void AddAppearanceColor(Panel parent, string property, string label)
    {
        var box = new TextBox();
        var swatch = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(7),
            BorderBrush = Kit.Brush("BorderBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        box.TextChanged += (_, _) =>
        {
            if (!AppearanceService.IsValid(box.Text)) return;
            swatch.Background = new SolidColorBrush(AppearanceService.Parse(box.Text, Colors.Transparent));
            ScheduleAppearanceAutosave();
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(box);
        Grid.SetColumn(swatch, 1);
        row.Children.Add(swatch);

        var field = Kit.Field(label, row);
        field.Margin = new Thickness(0, 0, 16, 10);
        parent.Children.Add(field);

        _appearanceTexts[property] = box;
        _appearanceSwatches[property] = swatch;
    }

    private void FillAppearance(AppearanceSettings settings)
    {
        _appearanceLoading = true;
        try
        {
            void Set(string property, string value)
            {
                if (_appearanceTexts.TryGetValue(property, out var box)) box.Text = value;
                if (_appearanceSwatches.TryGetValue(property, out var swatch))
                    swatch.Background = new SolidColorBrush(AppearanceService.Parse(value, Colors.Transparent));
            }

            Set(nameof(AppearanceSettings.Background), settings.Background);
            Set(nameof(AppearanceSettings.Surface), settings.Surface);
            Set(nameof(AppearanceSettings.SurfaceAlt), settings.SurfaceAlt);
            Set(nameof(AppearanceSettings.Border), settings.Border);
            Set(nameof(AppearanceSettings.Text), settings.Text);
            Set(nameof(AppearanceSettings.Muted), settings.Muted);
            Set(nameof(AppearanceSettings.Accent), settings.Accent);
            Set(nameof(AppearanceSettings.Accent2), settings.Accent2);
            Set(nameof(AppearanceSettings.Success), settings.Success);
            Set(nameof(AppearanceSettings.Warning), settings.Warning);
            Set(nameof(AppearanceSettings.Danger), settings.Danger);
            Set(nameof(AppearanceSettings.Purple), settings.Purple);

            _iconSizeSlider.Value = Math.Clamp(settings.NavIconSize, 12, 26);
            _navFontSlider.Value = Math.Clamp(settings.NavFontSize, 11, 17);
            _productCardSlider.Value = Math.Clamp(settings.ProductCardScale, 80, 160);
            _stationCardSlider.Value = Math.Clamp(settings.StationCardScale, 80, 160);
            var languageCode = L.Parse(settings.Language) == AppLanguage.English ? "en" : "es";
            _languageBox.SelectedItem = _languageBox.Items.Cast<Option<string>>()
                .FirstOrDefault(o => o.Value == languageCode);
            _appearancePresetBox.SelectedItem = AppearanceSettings.Presets
                .FirstOrDefault(p => p == settings.Preset) ?? AppearanceSettings.Presets[0];
        }
        finally
        {
            _appearanceLoading = false;
        }
    }

    private AppearanceSettings ReadAppearance()
    {
        string Get(string property, string label)
        {
            var text = _appearanceTexts[property].Text.Trim();
            if (!AppearanceService.IsValid(text))
                throw new InvalidOperationException($"El color «{label}» no es un valor hexadecimal válido (ejemplo #22D3EE).");
            return text;
        }

        return new AppearanceSettings
        {
            Preset = _appearancePresetBox.SelectedItem as string ?? "Personalizada",
            Background = Get(nameof(AppearanceSettings.Background), "fondo general"),
            Surface = Get(nameof(AppearanceSettings.Surface), "superficie de tarjetas"),
            SurfaceAlt = Get(nameof(AppearanceSettings.SurfaceAlt), "superficie alterna"),
            Border = Get(nameof(AppearanceSettings.Border), "bordes"),
            Text = Get(nameof(AppearanceSettings.Text), "texto principal"),
            Muted = Get(nameof(AppearanceSettings.Muted), "texto atenuado"),
            Accent = Get(nameof(AppearanceSettings.Accent), "acento principal"),
            Accent2 = Get(nameof(AppearanceSettings.Accent2), "acento secundario"),
            Success = Get(nameof(AppearanceSettings.Success), "éxito"),
            Warning = Get(nameof(AppearanceSettings.Warning), "advertencia"),
            Danger = Get(nameof(AppearanceSettings.Danger), "peligro"),
            Purple = Get(nameof(AppearanceSettings.Purple), "morado"),
            NavIconSize = _iconSizeSlider.Value,
            NavFontSize = _navFontSlider.Value,
            ProductCardScale = _productCardSlider.Value,
            StationCardScale = _stationCardSlider.Value,
            Language = (_languageBox.SelectedItem as Option<string>)?.Value ?? "es",

            // La personalización del tablero se administra desde el propio tablero:
            // se conserva al guardar colores, tamaños o idioma.
            DashboardSort = AppearanceService.Current.DashboardSort,
            DashboardSortDescending = AppearanceService.Current.DashboardSortDescending,
            DashboardShowMetrics = AppearanceService.Current.DashboardShowMetrics,
            DashboardShowLegend = AppearanceService.Current.DashboardShowLegend,
            DashboardOnlyInUse = AppearanceService.Current.DashboardOnlyInUse,
            DashboardZoneId = AppearanceService.Current.DashboardZoneId,
            DashboardSearch = AppearanceService.Current.DashboardSearch
        };
    }

    /// <summary>Conserva las preferencias del tablero al cambiar de tema predefinido.</summary>
    private static void CopyDashboardPreferences(AppearanceSettings from, AppearanceSettings to)
    {
        to.DashboardSort = from.DashboardSort;
        to.DashboardSortDescending = from.DashboardSortDescending;
        to.DashboardShowMetrics = from.DashboardShowMetrics;
        to.DashboardShowLegend = from.DashboardShowLegend;
        to.DashboardOnlyInUse = from.DashboardOnlyInUse;
        to.DashboardZoneId = from.DashboardZoneId;
        to.DashboardSearch = from.DashboardSearch;
    }

    private void ScheduleAppearanceAutosave()
    {
        if (_appearanceLoading) return;
        _appearanceStatus.Text = "Guardando cambios de apariencia...";
        _appearanceStatus.Foreground = Kit.Brush("MutedBrush");
        _appearanceAutosave.Stop();
        _appearanceAutosave.Start();
    }

    private void ApplyAppearance(bool previewOnly)
    {
        try
        {
            var settings = ReadAppearance();
            AppearanceService.Apply(settings, save: !previewOnly);
            _appearanceStatus.Text = previewOnly
                ? "Vista previa aplicada; se guardará automáticamente."
                : $"Apariencia guardada automáticamente a las {DateTime.Now:HH:mm:ss}.";
            _appearanceStatus.Foreground = Kit.Brush(previewOnly ? "MutedBrush" : "SuccessBrush");
        }
        catch (Exception ex)
        {
            _appearanceStatus.Text = ex.Message;
            _appearanceStatus.Foreground = Kit.Brush("DangerBrush");
        }
    }

    private void ResetAppearance()
    {
        _appearanceAutosave.Stop();
        var defaults = AppearanceSettings.ForPreset("Cyber");
        defaults.NavIconSize = _iconSizeSlider.Value;
        defaults.NavFontSize = _navFontSlider.Value;
        defaults.ProductCardScale = 100;
        defaults.StationCardScale = 100;
        defaults.Language = AppearanceService.Current.Language;
        CopyDashboardPreferences(AppearanceService.Current, defaults);
        FillAppearance(defaults);
        AppearanceService.Apply(defaults, save: true);
        _appearanceStatus.Text = "Apariencia restablecida a los valores originales.";
        _appearanceStatus.Foreground = Kit.Brush("SuccessBrush");
    }

    // ============================================================
    //  Respaldos
    // ============================================================
    private FrameworkElement BuildBackupTab()
    {
        var panel = new StackPanel();

        panel.Children.Add(Kit.Section("Respaldo automático"));
        panel.Children.Add(Description(
            "El servidor crea copias de seguridad de la base de datos sin detener la operación. " +
            "Los archivos se guardan junto a la base y se conservan según la cantidad indicada."));

        _backupFolderText.Margin = new Thickness(0, 0, 0, 10);
        panel.Children.Add(_backupFolderText);

        var auto = new UniformGrid { Columns = 2 };
        AddCheck(auto, "BackupAutoEnabled", "Crear respaldos automáticos", true);
        panel.Children.Add(auto);

        var frequency = new UniformGrid { Columns = 2 };
        AddText(frequency, "BackupIntervalHours", "Frecuencia (horas)", "24");
        AddText(frequency, "BackupRetention", "Respaldos automáticos a conservar", "30");
        panel.Children.Add(frequency);

        panel.Children.Add(Kit.Section("Acciones"));
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        actions.Children.Add(WithMargin(Kit.Button("Crear respaldo ahora", "PrimaryButton",
            () => _ = Ui.Run(CreateBackupAsync, "No se pudo crear el respaldo")), 0, 0, 8, 8));
        actions.Children.Add(WithMargin(Kit.Button("Exportar copia", "GhostButton",
            () => _ = Ui.Run(ExportBackupAsync, "No se pudo exportar el respaldo")), 0, 0, 8, 8));
        actions.Children.Add(WithMargin(Kit.Button("Restaurar seleccionado", "DangerButton",
            () => _ = Ui.Run(RestoreSelectedAsync, "No se pudo restaurar el respaldo")), 0, 0, 8, 8));
        actions.Children.Add(WithMargin(Kit.Button("Restaurar desde archivo", "DangerButton",
            () => _ = Ui.Run(RestoreFromFileAsync, "No se pudo restaurar el respaldo")), 0, 0, 8, 8));
        actions.Children.Add(WithMargin(Kit.Button("Actualizar lista", "GhostButton",
            () => _ = Ui.Run(LoadBackupsAsync, "No se pudo cargar la lista")), 0, 0, 8, 8));
        actions.Children.Add(WithMargin(Kit.Button("Abrir carpeta", "GhostButton", OpenBackupFolder),
            0, 0, 8, 8));
        panel.Children.Add(actions);

        _backupStatus.Margin = new Thickness(0, 0, 0, 12);
        panel.Children.Add(_backupStatus);

        panel.Children.Add(Kit.Section("Respaldos disponibles"));
        _backupsGrid.Height = 280;
        _backupsGrid.Margin = new Thickness(0, 0, 0, 12);
        _backupsGrid.MouseDoubleClick += (_, _) => _ = Ui.Run(RestoreSelectedAsync, "No se pudo restaurar el respaldo");
        panel.Children.Add(_backupsGrid);

        panel.Children.Add(Description(
            "Exportar copia descarga el respaldo a su equipo. Restaurar reemplaza la base actual y " +
            "crea antes un respaldo de seguridad llamado «antes_restaurar»."));

        return Scroll(panel);
    }

    private static FrameworkElement WithMargin(FrameworkElement element, double left, double top,
        double right, double bottom)
    {
        element.Margin = new Thickness(left, top, right, bottom);
        return element;
    }

    private async Task LoadBackupsAsync()
    {
        try
        {
            var list = await App.Api.BackupsAsync();
            _backupFolder = list.Folder;
            _backups = list.Files.Select(f => new BackupRow(f)).ToList();
            _backupsGrid.ItemsSource = _backups;
            _backupFolderText.Text = $"Carpeta en el servidor: {list.Folder}";
            _backupFolderText.ToolTip = list.Folder;

            var last = list.LastBackupUtc?.ToLocalTime();
            _backupStatus.Text = list.Files.Count == 0
                ? "Aún no hay respaldos. Cree uno ahora o espere el respaldo automático."
                : $"{list.Files.Count} respaldo(s). Último: {last:dd/MM/yyyy HH:mm}. " +
                  $"Automático: {(list.AutoBackupEnabled ? $"cada {list.IntervalHours} h" : "desactivado")}.";
            _backupStatus.Foreground = Kit.Brush(list.Files.Count == 0 ? "WarningBrush" : "MutedBrush");
        }
        catch (Exception ex)
        {
            _backupStatus.Text = ex.Message;
            _backupStatus.Foreground = Kit.Brush("DangerBrush");
        }
    }

    private async Task CreateBackupAsync()
    {
        if (_backupBusy) return;
        _backupBusy = true;
        _backupStatus.Text = "Creando respaldo...";
        _backupStatus.Foreground = Kit.Brush("MutedBrush");

        try
        {
            var file = await App.Api.CreateBackupAsync();
            await LoadBackupsAsync();
            _backupStatus.Text = $"Respaldo creado: {file.FileName} ({BackupRow.FormatSize(file.SizeBytes)}).";
            _backupStatus.Foreground = Kit.Brush("SuccessBrush");
        }
        catch (Exception ex)
        {
            _backupStatus.Text = ex.Message;
            _backupStatus.Foreground = Kit.Brush("DangerBrush");
        }
        finally
        {
            _backupBusy = false;
        }
    }

    private async Task ExportBackupAsync()
    {
        if (_backupBusy) return;

        try
        {
            BackupFileDto file;
            if (_backupsGrid.SelectedItem is BackupRow row)
            {
                file = row.Source;
            }
            else
            {
                _backupStatus.Text = "Creando respaldo para exportar...";
                _backupStatus.Foreground = Kit.Brush("MutedBrush");
                file = await App.Api.CreateBackupAsync();
            }

            var dialog = new SaveFileDialog
            {
                Title = "Exportar respaldo de la base de datos",
                FileName = file.FileName,
                DefaultExt = ".db",
                Filter = "Respaldo SQLite (*.db)|*.db|Todos los archivos (*.*)|*.*"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            var data = await App.Api.DownloadBackupAsync(file.FileName);
            await File.WriteAllBytesAsync(dialog.FileName, data);

            await LoadBackupsAsync();
            _backupStatus.Text = $"Copia exportada a {dialog.FileName}.";
            _backupStatus.Foreground = Kit.Brush("SuccessBrush");
        }
        catch (Exception ex)
        {
            _backupStatus.Text = ex.Message;
            _backupStatus.Foreground = Kit.Brush("DangerBrush");
        }
    }

    private async Task RestoreSelectedAsync()
    {
        if (_backupBusy) return;
        if (_backupsGrid.SelectedItem is not BackupRow row)
        {
            Ui.Info("Seleccione un respaldo de la lista.", "Restaurar base de datos");
            return;
        }

        if (!Ui.Confirm(
                $"¿Restaurar la base de datos desde «{row.FileName}»?\n\n" +
                "La base actual será reemplazada. Antes se creará un respaldo de seguridad.",
                "Restaurar base de datos"))
            return;

        await RestoreAsync(() => App.Api.RestoreStoredBackupAsync(row.FileName), row.FileName);
    }

    private async Task RestoreFromFileAsync()
    {
        if (_backupBusy) return;

        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar respaldo de CyberManager",
            Filter = "Respaldo SQLite (*.db)|*.db|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        if (!Ui.Confirm(
                $"¿Restaurar la base de datos desde «{Path.GetFileName(dialog.FileName)}»?\n\n" +
                "La base actual será reemplazada. Antes se creará un respaldo de seguridad.",
                "Restaurar base de datos"))
            return;

        var bytes = await File.ReadAllBytesAsync(dialog.FileName);
        await RestoreAsync(() => App.Api.RestoreBackupUploadAsync(Path.GetFileName(dialog.FileName), bytes),
            dialog.FileName);
    }

    private async Task RestoreAsync(Func<Task<RestoreBackupResultDto>> restore, string displayName)
    {
        _backupBusy = true;
        _backupStatus.Text = "Restaurando base de datos...";
        _backupStatus.Foreground = Kit.Brush("MutedBrush");

        try
        {
            var result = await restore();
            await RefreshAsync();
            _backupStatus.Text =
                $"Base de datos restaurada desde {displayName} a las {result.RestoredUtc.ToLocalTime():HH:mm:ss}. " +
                $"Respaldo previo: {result.SafetyBackupFile}.";
            _backupStatus.Foreground = Kit.Brush("SuccessBrush");
        }
        catch (Exception ex)
        {
            _backupStatus.Text = ex.Message;
            _backupStatus.Foreground = Kit.Brush("DangerBrush");
        }
        finally
        {
            _backupBusy = false;
        }
    }

    private void OpenBackupFolder()
    {
        if (string.IsNullOrWhiteSpace(_backupFolder))
        {
            Ui.Info("La carpeta de respaldos aún no está disponible.", "Respaldos");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = _backupFolder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message, "Respaldos");
        }
    }

    // ============================================================
    //  Instalación
    // ============================================================
    private void UninstallCyberManager()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var executable = Path.Combine(root, "CyberManager", "CyberManager.Uninstaller.exe");

        if (!File.Exists(executable))
        {
            Ui.Info("No se encontró el desinstalador independiente. " +
                    "Use «Aplicaciones instaladas» de Windows o el acceso directo del menú Inicio.",
                "Desinstalación");
            return;
        }

        if (!Ui.Confirm(
                "¿Desinstalar CyberManager de este equipo? Se cerrarán el servidor, la consola y el cliente. " +
                "La base de datos del negocio no se elimina.",
                "Desinstalar CyberManager"))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Ui.Error($"No se pudo iniciar el desinstalador: {ex.Message}", "Desinstalación");
        }
    }

    private FrameworkElement BuildInstallTab()
    {
        var panel = new StackPanel();
        panel.Children.Add(Kit.Section("Versión de CyberManager"));
        panel.Children.Add(Description($"{L.T("Versión instalada:")} {AppInfo.Version}"));

        panel.Children.Add(Kit.Section("Desinstalación"));
        panel.Children.Add(Description(
            "El desinstalador detiene el servidor, el agente y el cliente antes de quitar los archivos. " +
            "La base de datos del negocio no se elimina."));
        var uninstallButton = Kit.Button("Desinstalar CyberManager", "DangerButton", UninstallCyberManager);
        uninstallButton.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(uninstallButton);

        panel.Children.Add(Kit.Section("Arquitectura"));
        panel.Children.Add(Description(
            "CyberManager trabaja en red local. El servidor concentra la base de datos, la caja y las sesiones; " +
            "cada PC ejecuta el cliente WPF que bloquea Windows mientras no haya una sesión pagada. " +
            "El agente se instala como servicio para apagar, reiniciar, cerrar sesión y bloquear los equipos."));

        panel.Children.Add(Kit.Section("Pasos sugeridos"));
        var steps = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Text = "1. Ejecute el servidor en el PC principal y abra el puerto 5080 en el firewall.\n" +
                   "2. Instale la consola de administración y cambie la contraseña de admin.\n" +
                   "3. Registre la tasa del día y abra la caja antes de cobrar.\n" +
                   "4. Registre cada equipo y copie su ID y token en la configuración del cliente.\n" +
                   "5. Instale el agente como servicio en cada PC para las órdenes de energía.\n" +
                   "6. Configure el cliente para iniciar sesión automáticamente con Windows (modo kiosco)."
        };
        panel.Children.Add(steps);

        panel.Children.Add(Kit.Section("Datos de conexión del servidor"));
        AddCode(panel, "URL del servidor en esta consola", App.ServerUrl);
        AddCode(panel, "Regla de firewall sugerida (PowerShell como administrador)",
            "netsh advfirewall firewall add rule name=\"CyberManager\" dir=in action=allow protocol=TCP localport=5080");

        panel.Children.Add(Kit.Section("Agente de Windows"));
        panel.Children.Add(Description(
            "Publique el agente y registre el servicio con la ruta real del ejecutable. " +
            "El agente se conecta al servidor con el mismo ID y token del equipo."));
        AddCode(panel, "Crear el servicio (PowerShell como administrador)",
            "sc.exe create CyberManagerAgent binPath= \"C:\\CyberManager\\Agent\\CyberManager.Agent.exe\" start= auto\n" +
            "sc.exe description CyberManagerAgent \"CyberManager - agente de equipo\"\n" +
            "sc.exe start CyberManagerAgent");

        panel.Children.Add(Kit.Section("Inicio automático del cliente"));
        panel.Children.Add(Description(
            "Copie el acceso directo del cliente en la carpeta de inicio del usuario de kiosco, " +
            "o use el Programador de tareas para iniciarlo al iniciar sesión."));
        AddCode(panel, "Carpeta de inicio del usuario actual",
            "%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\Startup");

        panel.Children.Add(Kit.Section("Credenciales de demostración"));
        panel.Children.Add(Description(
            "admin / admin123 (administrador) · cajero / cajero123 (cajero). " +
            "Cambie estas contraseñas antes de usar el sistema en producción."));

        panel.Children.Add(Kit.Section("Nota sobre IGTF"));
        panel.Children.Add(Description(
            "El IGTF (3% por defecto) se aplica solo cuando el pago se registra en efectivo en dólares. " +
            "Puede desactivarlo o cambiar su porcentaje en la pestaña «Negocio y facturación». " +
            "Los precios se capturan con IVA incluido y el sistema desglosa el impuesto al facturar."));

        return Scroll(panel);
    }

    private static void AddCode(Panel parent, string title, string code)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title,
            Style = Kit.Style("Label"),
            Margin = new Thickness(0, 6, 0, 4)
        });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = new TextBox
        {
            Text = code,
            IsReadOnly = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        row.Children.Add(box);

        var copy = Kit.SmallButton("Copiar", "GhostButton", () =>
        {
            Clipboard.SetText(code);
            Ui.Info("Texto copiado al portapapeles.", "Instalación");
        });
        copy.Margin = new Thickness(8, 0, 0, 0);
        copy.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);

        row.Margin = new Thickness(0, 0, 0, 8);
        parent.Children.Add(row);
    }

    // ============================================================
    //  Carga general
    // ============================================================
    public async Task RefreshAsync()
    {
        _businessLoaded = false;
        _businessAutosave.Stop();

        var settings = await App.Api.SettingsAsync();

        foreach (var (key, box) in _texts)
            box.Text = settings.TryGetValue(key, out var value)
                ? value
                : _textDefaults.GetValueOrDefault(key, string.Empty);

        foreach (var (key, check) in _checks)
            check.IsChecked = settings.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed)
                ? parsed
                : _checkDefaults.GetValueOrDefault(key, true);

        _businessLoaded = true;
        _businessStatus.Text = $"Configuración cargada · {settings.Count} claves · autoguardado activo";
        _businessStatus.Foreground = Kit.Brush("MutedBrush");

        await LoadRatesAsync();
        await LoadUsersAsync();
        await LoadBackupsAsync();
    }

    // ============================================================
    //  Constructores de campos
    // ============================================================
    private void AddText(Panel parent, string key, string label, string fallback,
        bool multiline = false, bool fullWidth = false)
    {
        var box = multiline ? Kit.Multiline(null, fullWidth ? 72 : 60) : Kit.Input();
        _texts[key] = box;
        _textDefaults[key] = fallback;
        box.TextChanged += (_, _) => ScheduleBusinessAutosave();

        var field = Kit.Field(label, box);
        field.Margin = new Thickness(0, 0, fullWidth ? 0 : 16, 10);
        parent.Children.Add(field);
    }

    private void AddCheck(Panel parent, string key, string label, bool fallback)
    {
        var check = new CheckBox
        {
            Content = label,
            Margin = new Thickness(0, 4, 16, 4),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _checks[key] = check;
        _checkDefaults[key] = fallback;
        check.Checked += (_, _) => ScheduleBusinessAutosave();
        check.Unchecked += (_, _) => ScheduleBusinessAutosave();
        parent.Children.Add(check);
    }

    private static TextBlock Description(string text)
        => new()
        {
            Text = text,
            Style = Kit.Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

    private static ScrollViewer Scroll(UIElement content)
        => new()
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 14, 0, 0)
        };

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        if (decimal.TryParse((text ?? string.Empty).Trim(), NumberStyles.Any,
                CultureInfo.CurrentCulture, out value))
            return true;

        return decimal.TryParse((text ?? string.Empty).Trim(), NumberStyles.Any,
            CultureInfo.InvariantCulture, out value);
    }
}

public sealed class BackupRow
{
    public BackupRow(BackupFileDto file)
    {
        Source = file;
        Date = file.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        Kind = file.Automatic
            ? "Automático"
            : file.FileName.Contains("antes_restaurar", StringComparison.OrdinalIgnoreCase)
                ? "Antes de restaurar"
                : "Manual";
        Size = FormatSize(file.SizeBytes);
        FileName = file.FileName;
    }

    public BackupFileDto Source { get; }
    public string Date { get; }
    public string Kind { get; }
    public string Size { get; }
    public string FileName { get; }

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024L) return $"{bytes / 1024d / 1024d:N1} MB";
        if (bytes >= 1024L) return $"{bytes / 1024d:N0} KB";
        return $"{bytes} B";
    }
}

public sealed class RateHistoryRow
{
    public RateHistoryRow(ExchangeRateDto rate)
    {
        Date = rate.Date.ToString("dd/MM/yyyy");
        Rate = rate.Rate;
        Source = Labels.RateSourceName(rate.Source);
        Active = rate.IsActive ? "Sí" : "No";
        Created = rate.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Notes = string.IsNullOrWhiteSpace(rate.Notes) ? "—" : rate.Notes!;
    }

    public string Date { get; }
    public decimal Rate { get; }
    public string Source { get; }
    public string Active { get; }
    public string Created { get; }
    public string Notes { get; }
}

public sealed class UserRow
{
    public UserRow(UserDto user)
    {
        Source = user;
        Username = user.Username;
        FullName = user.FullName;
        Role = Labels.RoleName(user.Role);
        Status = user.Active ? "Activo" : "Inactivo";
        LastLogin = user.LastLoginUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
    }

    public UserDto Source { get; }
    public string Username { get; }
    public string FullName { get; }
    public string Role { get; }
    public string Status { get; }
    public string LastLogin { get; }
}

public sealed class UserDialog : FormDialog
{
    private readonly UserDto? _user;
    private readonly TextBox _username;
    private readonly TextBox _fullName;
    private readonly ComboBox _roleBox;
    private readonly PasswordBox _password;
    private readonly CheckBox? _active;

    public CreateUserRequest? CreateRequest { get; private set; }
    public UpdateUserRequest? UpdateRequest { get; private set; }

    public UserDialog(UserDto? user = null)
        : base(user is null ? "Nuevo usuario" : $"Editar {user.Username}",
            user is null
                ? "Cree un usuario con acceso a la consola de administración."
                : "Deje la contraseña vacía para conservarla.")
    {
        _user = user;

        _username = Add(new TextBox { Text = user?.Username ?? string.Empty, IsReadOnly = user is not null },
            "Nombre de usuario");
        _fullName = Add(new TextBox { Text = user?.FullName ?? string.Empty }, "Nombre completo");

        var roleOptions = new List<Option<UserRole>>
        {
            new(UserRole.Admin, "Administrador"),
            new(UserRole.Cashier, "Cajero"),
            new(UserRole.Technician, "Técnico")
        };
        _roleBox = AddCombo("Rol", roleOptions);
        if (user is not null)
        {
            var index = roleOptions.FindIndex(o => o.Value == user.Role);
            if (index >= 0) _roleBox.SelectedIndex = index;
        }

        _password = Add(new PasswordBox { Height = 36 }, user is null ? "Contraseña (mínimo 6 caracteres)" : "Nueva contraseña (opcional)");

        if (user is not null)
            _active = AddCheck("Usuario activo", user.Active);

        AddNote("El administrador puede modificar configuración, usuarios, compras y reportes.");
    }

    protected override void OnAccept()
    {
        var role = ((Option<UserRole>)_roleBox.SelectedItem!).Value;
        var password = _password.Password;

        if (string.IsNullOrWhiteSpace(_fullName.Text))
            throw new InvalidOperationException("Indique el nombre completo.");

        if (_user is null)
        {
            if (string.IsNullOrWhiteSpace(_username.Text))
                throw new InvalidOperationException("Indique el nombre de usuario.");
            if (password.Length < 6)
                throw new InvalidOperationException("La contraseña debe tener al menos 6 caracteres.");

            CreateRequest = new CreateUserRequest(_username.Text.Trim(), password, _fullName.Text.Trim(), role);
        }
        else
        {
            UpdateRequest = new UpdateUserRequest(_fullName.Text.Trim(), role,
                _active?.IsChecked == true, string.IsNullOrWhiteSpace(password) ? null : password);
        }

        base.OnAccept();
    }
}

