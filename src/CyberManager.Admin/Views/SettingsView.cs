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
    private readonly Dictionary<string, ComboBox> _printers = new();
    private readonly Dictionary<string, string> _textDefaults = new();
    private readonly Dictionary<string, string> _printerDefaults = new();
    private readonly Dictionary<string, bool> _checkDefaults = new();

    private readonly TextBlock _businessStatus = Kit.Muted("Cargue la configuración para comenzar.");
    private readonly TextBlock _saveStatus = Kit.Muted("Los cambios se guardan con «Aplicar y guardar».");
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
    private readonly TextBlock _updateDetail = Kit.Muted("Consultando la última versión publicada...");
    private readonly TextBlock _updateStatus = Kit.Muted("El servidor consulta GitHub automáticamente.");
    private readonly TextBlock _emailStatus = Kit.Muted("El envío del cierre diario por correo está desactivado.");
    private readonly PasswordBox _smtpPassword = new();
    private bool _clearSmtpPassword;
    private bool _updateBusy;

    private readonly Dictionary<string, TextBox> _appearanceTexts = new();
    private readonly Dictionary<string, Border> _appearanceSwatches = new();
    private readonly TextBlock _appearanceStatus = Kit.Muted("Pulse «Aplicar y guardar» para conservar los cambios.");
    private ComboBox _appearancePresetBox = null!;
    private ComboBox _languageBox = null!;
    private ComboBox _closingFormatBox = null!;
    private Slider _iconSizeSlider = null!;
    private Slider _navFontSlider = null!;
    private Slider _productCardSlider = null!;
    private Slider _stationCardSlider = null!;
    private bool _appearanceLoading;
    private bool _businessLoaded;
    private bool _businessSaving;
    private bool _backupBusy;
    private bool _bcvBusy;

    private static readonly (string Value, string Label)[] ClosingFormats =
    {
        ("ticket80", "Ticket 80 mm (térmico)"),
        ("a4", "A4 (210 × 297 mm)"),
        ("carta", "Carta (216 × 279 mm)")
    };

    private List<UserDto> _users = new();
    private List<BackupRow> _backups = new();
    private string? _backupFolder;
    private readonly DataGrid _webFilterGrid;
    private readonly TextBlock _webFilterStatus = Kit.Muted("Cargando la lista de NetFiltroWeb...");
    private List<WebFilterRuleDto> _webFilterRules = new();
    private bool _webFilterBusy;

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

        _webFilterGrid = Kit.Table(
            Kit.Col("Sitio o palabra", nameof(WebFilterRow.Pattern), 250),
            Kit.Col("Tipo", nameof(WebFilterRow.Kind), 130),
            Kit.Col("Estado", nameof(WebFilterRow.Status), 100),
            Kit.Col("Notas", nameof(WebFilterRow.Notes)));

        var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        tabs.Items.Add(new TabItem { Header = "Negocio y facturación", Content = BuildBusinessTab() });
        tabs.Items.Add(new TabItem { Header = "Tasa de cambio", Content = BuildRateTab() });
        tabs.Items.Add(new TabItem { Header = "Usuarios", Content = BuildUsersTab() });
        tabs.Items.Add(new TabItem { Header = "Apariencia", Content = BuildAppearanceTab() });
        tabs.Items.Add(new TabItem { Header = "Respaldos", Content = BuildBackupTab() });
        tabs.Items.Add(new TabItem { Header = "Reportes por correo", Content = BuildEmailTab() });
        tabs.Items.Add(new TabItem { Header = "Control remoto y filtro", Content = BuildRemoteFilterTab() });
        tabs.Items.Add(new TabItem { Header = "Instalación", Content = BuildInstallTab() });

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(Kit.PageHeader("Configuración",
            "Datos fiscales, tasa del día, usuarios, apariencia y respaldos de la base de datos.",
            Kit.Button("Aplicar y guardar", "SuccessButton", () => _ = SaveAllAsync()),
            Kit.Button("Recargar", "GhostButton", () => _ = Ui.Run(RefreshAsync, "No se pudo cargar la configuración"))));
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        _saveStatus.Margin = new Thickness(0, 8, 0, 0);
        _saveStatus.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(_saveStatus, 2);
        root.Children.Add(_saveStatus);

        Content = root;

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
        AddPrinter(operation, "PrinterName", "Impresora de tickets (vacío = predeterminada)", "");
        AddPrinter(operation, "WorkPrinterName", "Impresora de trabajo (cierres y reportes)", "");
        AddText(operation, "TicketCopies", "Copias por ticket (1 a 5)", "1");
        panel.Children.Add(operation);

        var closingFormat = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 10) };
        _closingFormatBox = new ComboBox
        {
            ItemsSource = ClosingFormats.Select(f => new Option<string>(f.Value, f.Label)).ToList(),
            DisplayMemberPath = nameof(Option<object>.DisplayText),
            SelectedIndex = 0
        };
        closingFormat.Children.Add(Kit.Field("Formato del cierre diario", _closingFormatBox));
        var closingHint = Kit.Muted(
            "Ticket 80 mm para impresoras térmicas; A4 o Carta para hojas normales.");
        closingHint.VerticalAlignment = VerticalAlignment.Bottom;
        closingHint.Margin = new Thickness(0, 0, 0, 14);
        closingFormat.Children.Add(closingHint);
        panel.Children.Add(closingFormat);

        AddText(panel, "TicketHeader", "Encabezado del ticket (opcional)",
            "", fullWidth: true);

        var ticketChecks = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 8) };
        AddCheck(ticketChecks, "AutoPrintTicket", "Imprimir ticket automáticamente al cobrar", false);
        AddCheck(ticketChecks, "ScannerAutoAdd", "El lector de códigos agrega al presionar Enter", true);
        panel.Children.Add(ticketChecks);

        AddText(panel, "TicketFooter", "Pie de página del ticket",
            "Gracias por su visita. ¡Vuelva pronto!", multiline: true, fullWidth: true);

        var save = Kit.Button("Aplicar y guardar", "SuccessButton", () => _ = SaveAllAsync());
        save.Padding = new Thickness(16, 11, 16, 11);
        save.Margin = new Thickness(0, 8, 0, 0);
        save.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(save);

        panel.Children.Add(Description(
            "Los cambios se mantienen como borrador hasta que pulse «Aplicar y guardar»."));

        _businessStatus.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(_businessStatus);

        return Scroll(panel);
    }

    /// <summary>Guarda la configuración del servidor y la apariencia local en una sola acción.</summary>
    private async Task SaveAllAsync()
    {
        if (!_businessLoaded) return;
        if (_businessSaving) return;

        // Valide la apariencia antes de guardar para no dejar una parte a medias.
        AppearanceSettings appearance;
        try
        {
            appearance = ReadAppearance();
        }
        catch (Exception ex)
        {
            _appearanceStatus.Text = ex.Message;
            _appearanceStatus.Foreground = Kit.Brush("DangerBrush");
            _saveStatus.Text = "No se guardó nada: " + ex.Message;
            _saveStatus.Foreground = Kit.Brush("DangerBrush");
            return;
        }

        var values = CollectBusinessValues();

        _businessSaving = true;
        _businessStatus.Text = "Guardando...";
        _businessStatus.Foreground = Kit.Brush("MutedBrush");
        _saveStatus.Text = "Guardando la configuración...";
        _saveStatus.Foreground = Kit.Brush("MutedBrush");

        try
        {
            await App.Api.SaveSettingsAsync(values);
            AppearanceService.Apply(appearance, save: true);

            _clearSmtpPassword = false;
            _smtpPassword.Password = string.Empty;

            var time = DateTime.Now.ToString("HH:mm:ss");
            _businessStatus.Text = $"Configuración guardada a las {time}.";
            _businessStatus.Foreground = Kit.Brush("SuccessBrush");
            _appearanceStatus.Text = $"Apariencia guardada a las {time}.";
            _appearanceStatus.Foreground = Kit.Brush("SuccessBrush");
            _saveStatus.Text = $"Configuración y apariencia guardadas a las {time}.";
            _saveStatus.Foreground = Kit.Brush("SuccessBrush");
        }
        catch (Exception ex)
        {
            _businessStatus.Text = ex.Message;
            _businessStatus.Foreground = Kit.Brush("DangerBrush");
            _saveStatus.Text = "No se pudo guardar: " + ex.Message;
            _saveStatus.Foreground = Kit.Brush("DangerBrush");
        }
        finally
        {
            _businessSaving = false;
        }
    }

    private Dictionary<string, string> CollectBusinessValues()
    {
        var values = new Dictionary<string, string>();

        foreach (var (key, box) in _texts)
            values[key] = Normalize(box.Text);

        foreach (var (key, box) in _printers)
            values[key] = box.Text.Trim();

        foreach (var (key, check) in _checks)
            values[key] = (check.IsChecked == true).ToString().ToLowerInvariant();

        values["DailyClosingFormat"] =
            (_closingFormatBox.SelectedItem as Option<string>)?.Value ?? "ticket80";

        // La contraseña guardada no se devuelve al cliente. Solo se envía cuando el
        // administrador escribe una nueva; dejarla vacía conserva la existente.
        if (_clearSmtpPassword)
            values["SmtpPassword"] = string.Empty;
        else if (!string.IsNullOrEmpty(_smtpPassword.Password))
            values["SmtpPassword"] = _smtpPassword.Password;

        return values;
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
            Kit.Button("Desactivar", "GhostButton", DeactivateUser),
            Kit.Button("Eliminar", "DangerButton", DeleteUserPermanently));
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

    private void DeleteUserPermanently()
    {
        if (_usersGrid.SelectedItem is not UserRow row)
        {
            Ui.Info("Seleccione un usuario de la lista.", "Usuarios");
            return;
        }

        if (row.Source.Id == App.Session.UserId)
        {
            Ui.Info("No puede eliminar su propio usuario.", "Eliminar usuario");
            return;
        }

        if (!Ui.Confirm(
                $"¿Eliminar permanentemente al usuario {row.Username}? La cuenta se borrará y no podrá " +
                "iniciar sesión de nuevo. Las ventas, cajas y movimientos anteriores conservan su historial.",
                "Eliminar usuario"))
            return;

        _ = Ui.Run(async () =>
        {
            await App.Api.DeleteUserPermanentlyAsync(row.Source.Id);
            await LoadUsersAsync();
        }, "No se pudo eliminar el usuario");
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
            "Los cambios se mantienen como vista previa hasta pulsar «Aplicar y guardar»."));

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
            MarkAppearanceDirty();
        };

        var languageRow = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 12) };
        languageRow.Children.Add(Kit.Field("Idioma de la interfaz", _languageBox));
        languageRow.Children.Add(Kit.Muted("Solo se admiten español e inglés. Se aplica al guardar."));
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
            preset.Language = (_languageBox.SelectedItem as Option<string>)?.Value
                              ?? AppearanceService.Current.Language;
            CopyDashboardPreferences(AppearanceService.Current, preset);
            FillAppearance(preset);
            ApplyAppearance(previewOnly: true);
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
        };
        sizes.Children.Add(Kit.Field("Tamaño de iconos (12 a 26)", _iconSizeSlider));
        _navFontSlider = MakeSlider(11, 17, 13);
        _navFontSlider.ValueChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            ApplyAppearance(previewOnly: true);
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
        };
        cards.Children.Add(Kit.Field("Cuadros de artículos (80 a 160 %)", _productCardSlider));
        _stationCardSlider = MakeSlider(80, 160, 100, 5);
        _stationCardSlider.ValueChanged += (_, _) =>
        {
            if (_appearanceLoading) return;
            ApplyAppearance(previewOnly: true);
        };
        cards.Children.Add(Kit.Field("Cuadros de terminales (80 a 160 %)", _stationCardSlider));
        panel.Children.Add(cards);

        var actions = Kit.HStack(8,
            Kit.Button("Aplicar y guardar", "SuccessButton", () => _ = SaveAllAsync()),
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
            MarkAppearanceDirty();
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

    private void MarkAppearanceDirty()
    {
        if (_appearanceLoading) return;
        _appearanceStatus.Text = "Cambios de apariencia sin guardar. Pulse «Aplicar y guardar».";
        _appearanceStatus.Foreground = Kit.Brush("WarningBrush");
    }

    private void ApplyAppearance(bool previewOnly)
    {
        try
        {
            var settings = ReadAppearance();
            // El idioma solo cambia al guardar: así una vista previa no reconstruye
            // la consola ni descarta los demás cambios sin guardar.
            if (previewOnly) settings.Language = AppearanceService.Current.Language;
            AppearanceService.Apply(settings, save: !previewOnly);
            _appearanceStatus.Text = previewOnly
                ? "Vista previa aplicada; pulse «Aplicar y guardar» para conservarla."
                : $"Apariencia guardada a las {DateTime.Now:HH:mm:ss}.";
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
        var defaults = AppearanceSettings.ForPreset("Cyber");
        defaults.NavIconSize = _iconSizeSlider.Value;
        defaults.NavFontSize = _navFontSlider.Value;
        defaults.ProductCardScale = 100;
        defaults.StationCardScale = 100;
        defaults.Language = (_languageBox.SelectedItem as Option<string>)?.Value
                            ?? AppearanceService.Current.Language;
        CopyDashboardPreferences(AppearanceService.Current, defaults);
        FillAppearance(defaults);
        ApplyAppearance(previewOnly: true);
        _appearanceStatus.Text =
            "Apariencia restablecida en vista previa. Pulse «Aplicar y guardar» para conservarla.";
        _appearanceStatus.Foreground = Kit.Brush("WarningBrush");
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

    // ============================================================
    //  Reportes por correo
    // ============================================================
    private FrameworkElement BuildEmailTab()
    {
        var panel = new StackPanel();

        panel.Children.Add(Kit.Section("Cierre diario por correo"));
        panel.Children.Add(Description(
            "Cuando esta opción está activa, al abrir el cierre diario el servidor envía el resumen " +
            "del día a los destinatarios indicados. Si el correo falla, el cierre y la caja siguen " +
            "funcionando normalmente. El envío se realiza una sola vez por fecha; use «Reenviar» " +
            "en el cierre diario para repetirlo."));

        var enabled = new UniformGrid { Columns = 1 };
        AddCheck(enabled, "DailyReportEmailEnabled",
            "Enviar automáticamente el cierre diario por correo", false);
        panel.Children.Add(enabled);

        AddText(panel, "DailyReportEmailRecipients",
            "Destinatarios (separados por coma o punto y coma)", "", fullWidth: true);

        panel.Children.Add(Kit.Section("Servidor SMTP"));
        panel.Children.Add(Description(
            "Use los datos del proveedor de correo. Ejemplos: Gmail (smtp.gmail.com, puerto 587, SSL activado, " +
            "contraseña de aplicación), Outlook (smtp.office365.com, puerto 587) o el servidor SMTP del negocio. " +
            "La contraseña se guarda en la base de datos del servidor."));

        var smtp = new UniformGrid { Columns = 2 };
        AddText(smtp, "SmtpHost", "Servidor SMTP", "");
        AddText(smtp, "SmtpPort", "Puerto", "587");
        AddText(smtp, "SmtpUsername", "Usuario SMTP", "");
        AddText(smtp, "SmtpFromAddress", "Correo del remitente", "");
        AddText(smtp, "SmtpFromName", "Nombre del remitente", "CyberManager");
        panel.Children.Add(smtp);

        _smtpPassword.PasswordChanged += (_, _) =>
        {
            if (_businessSaving) return;
            if (!string.IsNullOrEmpty(_smtpPassword.Password)) _clearSmtpPassword = false;
            if (!_businessLoaded) return;
            _emailStatus.Text = "Cambios sin guardar. Pulse «Aplicar y guardar».";
            _emailStatus.Foreground = Kit.Brush("WarningBrush");
        };
        var passwordField = Kit.Field("Contraseña SMTP (deje vacío para conservar la actual)", _smtpPassword);
        passwordField.Margin = new Thickness(0, 0, 16, 10);
        panel.Children.Add(passwordField);

        var clearPassword = Kit.Button("Borrar contraseña guardada", "DangerButton", ClearSmtpPassword);
        clearPassword.HorizontalAlignment = HorizontalAlignment.Left;
        clearPassword.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(clearPassword);

        var ssl = new UniformGrid { Columns = 1 };
        AddCheck(ssl, "SmtpUseSsl", "Usar conexión segura SSL/TLS (recomendado)", true);
        panel.Children.Add(ssl);

        var actions = Kit.HStack(8,
            Kit.Button("Aplicar y guardar", "SuccessButton", () => _ = SaveAllAsync()),
            Kit.Button("Enviar correo de prueba", "GhostButton", () => _ = TestEmailAsync()));
        actions.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(actions);

        _emailStatus.Margin = new Thickness(0, 10, 0, 0);
        _emailStatus.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_emailStatus);

        panel.Children.Add(Description(
            "El correo incluye el resumen del cierre en el cuerpo y el detalle en un archivo CSV adjunto. " +
            "Verifique la carpeta de correo no deseado si no llega el primer envío."));

        return Scroll(panel);
    }

    private void ClearSmtpPassword()
    {
        _clearSmtpPassword = true;
        _smtpPassword.Password = string.Empty;
        _emailStatus.Text = "La contraseña SMTP se borrará al pulsar «Aplicar y guardar».";
        _emailStatus.Foreground = Kit.Brush("WarningBrush");
    }

    private async Task TestEmailAsync()
    {
        _emailStatus.Text = "Enviando el correo de prueba con los valores actuales del formulario...";
        _emailStatus.Foreground = Kit.Brush("MutedBrush");

        try
        {
            var port = int.TryParse(_texts["SmtpPort"].Text, out var parsedPort)
                ? parsedPort
                : (int?)null;

            var request = new TestEmailRequest(
                _texts["DailyReportEmailRecipients"].Text,
                _texts["SmtpHost"].Text.Trim(),
                port,
                _checks["SmtpUseSsl"].IsChecked == true,
                _texts["SmtpUsername"].Text.Trim(),
                _smtpPassword.Password,
                _texts["SmtpFromAddress"].Text.Trim(),
                _texts["SmtpFromName"].Text.Trim());

            var result = await App.Api.SendTestEmailAsync(request);
            _emailStatus.Text = result.Message + " (sin guardar los cambios)";
            _emailStatus.Foreground = Kit.Brush("SuccessBrush");
        }
        catch (Exception ex)
        {
            _emailStatus.Text = ex.Message;
            _emailStatus.Foreground = Kit.Brush("DangerBrush");
        }
    }

    private FrameworkElement BuildRemoteFilterTab()
    {
        var panel = new StackPanel();

        panel.Children.Add(Kit.Section("Barra flotante del cliente"));
        panel.Children.Add(Description(
            "La barra muestra el tiempo y el monto de la sesión sobre el escritorio del cliente. " +
            "Puede cambiar el texto descriptivo y el tamaño sin reiniciar los equipos."));
        var bar = new UniformGrid { Columns = 2 };
        AddText(bar, "SessionBarDescription", "Descripción de la barra",
            "Sesión activa · consulte su tiempo y consumo");
        AddText(bar, "SessionBarScale", "Tamaño de la barra (%)", "100");
        panel.Children.Add(bar);

        panel.Children.Add(Kit.Section("Control remoto VNC"));
        panel.Children.Add(Description(
            "El control remoto integrado de «Ver pantalla remota» funciona sin software adicional. " +
            "Si instaló TightVNC con el instalador, la consola detecta el visor automáticamente; " +
            "también puede indicar aquí una ruta personalizada. El puerto debe coincidir con el " +
            "configurado en la estación y el Firewall solo permite la red local."));
        var vnc = new UniformGrid { Columns = 2 };
        AddText(vnc, "VncViewerPath", "Ruta del visor VNC (opcional)", "");
        AddText(vnc, "VncDefaultPort", "Puerto VNC por defecto", "5900");
        panel.Children.Add(vnc);

        panel.Children.Add(Kit.Section("NetFiltroWeb"));
        panel.Children.Add(Description(
            "Lista de sitios y palabras prohibidas. Los equipos descargan la lista automáticamente. " +
            "Los sitios se bloquean en el archivo hosts; las palabras clave usan el proxy local del agente."));
        var proxy = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 8) };
        AddCheck(proxy, "WebFilterProxyEnabled",
            "Activar proxy local para bloquear palabras clave", true);
        AddText(proxy, "WebFilterProxyPort", "Puerto del proxy local", "8877");
        panel.Children.Add(proxy);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 8)
        };
        buttons.Children.Add(Kit.Button("Agregar regla", "PrimaryButton", AddWebFilterRule));
        var edit = Kit.Button("Editar", "GhostButton", EditWebFilterRule);
        edit.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(edit);
        var delete = Kit.Button("Eliminar", "DangerButton", DeleteWebFilterRule);
        delete.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(delete);
        var restore = Kit.Button("Restaurar lista de fábrica", "GhostButton",
            () => _ = Ui.Run(RestoreWebFilterAsync, "NetFiltroWeb"));
        restore.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(restore);
        panel.Children.Add(buttons);

        _webFilterGrid.Height = 280;
        _webFilterGrid.MouseDoubleClick += (_, _) => EditWebFilterRule();
        panel.Children.Add(_webFilterGrid);

        _webFilterStatus.Margin = new Thickness(0, 8, 0, 0);
        _webFilterStatus.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_webFilterStatus);

        var save = Kit.Button("Aplicar y guardar", "SuccessButton", () => _ = SaveAllAsync());
        save.Padding = new Thickness(16, 11, 16, 11);
        save.Margin = new Thickness(0, 12, 0, 0);
        save.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(save);

        return Scroll(panel);
    }

    private void AddWebFilterRule()
    {
        var dialog = new WebFilterRuleDialog(null) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        _ = Ui.Run(async () =>
        {
            await App.Api.SaveWebFilterRuleAsync(null, dialog.Result);
            await LoadWebFilterAsync();
        }, "NetFiltroWeb");
    }

    private void EditWebFilterRule()
    {
        if (_webFilterGrid.SelectedItem is not WebFilterRow row)
        {
            Ui.Info("Seleccione una regla de la lista.", "NetFiltroWeb");
            return;
        }

        var dialog = new WebFilterRuleDialog(row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        _ = Ui.Run(async () =>
        {
            await App.Api.SaveWebFilterRuleAsync(row.Source.Id, dialog.Result);
            await LoadWebFilterAsync();
        }, "NetFiltroWeb");
    }

    private void DeleteWebFilterRule()
    {
        if (_webFilterGrid.SelectedItem is not WebFilterRow row)
        {
            Ui.Info("Seleccione una regla de la lista.", "NetFiltroWeb");
            return;
        }

        if (!Ui.Confirm($"¿Eliminar la regla «{row.Pattern}»?", "NetFiltroWeb")) return;
        _ = Ui.Run(async () =>
        {
            await App.Api.DeleteWebFilterRuleAsync(row.Source.Id);
            await LoadWebFilterAsync();
        }, "NetFiltroWeb");
    }

    private async Task RestoreWebFilterAsync()
    {
        if (!Ui.Confirm("¿Agregar de nuevo las reglas de fábrica que falten?", "NetFiltroWeb")) return;
        await App.Api.RestoreWebFilterDefaultsAsync();
        await LoadWebFilterAsync();
    }

    private async Task LoadWebFilterAsync()
    {
        if (_webFilterBusy) return;
        _webFilterBusy = true;
        try
        {
            _webFilterRules = await App.Api.WebFilterRulesAsync();
            _webFilterGrid.ItemsSource = _webFilterRules.Select(r => new WebFilterRow(r)).ToList();
            var active = _webFilterRules.Count(r => r.Active);
            _webFilterStatus.Text =
                $"{_webFilterRules.Count} regla(s) · {active} activa(s). Los equipos se actualizan solos.";
            _webFilterStatus.Foreground = Kit.Brush("MutedBrush");
        }
        catch (Exception ex)
        {
            _webFilterStatus.Text = ex.Message;
            _webFilterStatus.Foreground = Kit.Brush("DangerBrush");
        }
        finally
        {
            _webFilterBusy = false;
        }
    }

    private static FrameworkElement WithMargin(FrameworkElement element, double left, double top,
        double right, double bottom)    {
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

        panel.Children.Add(Kit.Section("Actualizaciones"));
        panel.Children.Add(Description(
            "CyberManager consulta las versiones publicadas en GitHub una sola vez, al abrir la consola. " +
            "Cuando aparece una versión nueva, la consola pregunta si desea actualizar y, si acepta, " +
            "abre la página de versiones del proyecto. Use «Buscar actualizaciones ahora» si desea " +
            "comprobarlo manualmente."));

        var reminder = new UniformGrid { Columns = 1 };
        AddCheck(reminder, "UpdateReminderEnabled",
            "Avisar cuando haya una nueva versión disponible", true);
        panel.Children.Add(reminder);

        var reminderFields = new UniformGrid { Columns = 2 };
        AddText(reminderFields, "UpdateReminderDelayHours",
            "Horas de espera después de la publicación", "2");
        panel.Children.Add(reminderFields);

        _updateDetail.Margin = new Thickness(0, 2, 0, 4);
        _updateDetail.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_updateDetail);

        _updateStatus.Margin = new Thickness(0, 0, 0, 10);
        _updateStatus.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_updateStatus);

        var checkUpdates = Kit.Button("Buscar actualizaciones ahora", "GhostButton",
            () => _ = Ui.Run(CheckUpdatesAsync, "No se pudo consultar GitHub"));
        checkUpdates.HorizontalAlignment = HorizontalAlignment.Left;
        checkUpdates.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(checkUpdates);

        panel.Children.Add(Description(
            "El aviso aparece al abrir la consola una vez transcurridas las horas de espera " +
            "(2 por defecto). Puede desactivarlo o cambiar la espera; al responder «No» se oculta " +
            "ese aviso hasta la próxima versión."));

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
                   "6. Configure el número máximo de equipos permitidos en esta consola."
        };
        panel.Children.Add(steps);

        panel.Children.Add(Kit.Section("Límite de equipos"));
        panel.Children.Add(Description(
            "Número máximo de equipos que pueden registrarse en el servidor. " +
            "Si el límite se alcanza, no se podrán agregar más equipos hasta aumentar el límite o desactivar algunos."));
        var maxStationsGrid = new UniformGrid { Columns = 2 };
        var maxStationsBox = new TextBox
        {
            Text = "10",
            Width = 100,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            ToolTip = "Número máximo de equipos (estaciones) permitidos en el servidor"
        };
        AddText(maxStationsGrid, "MaxStations", "Máximo de equipos", "10");
        maxStationsGrid.Children.Add(maxStationsBox);
        panel.Children.Add(maxStationsGrid);

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
    //  Actualizaciones
    // ============================================================
    private async Task LoadUpdateAsync()
    {
        try
        {
            ShowUpdate(await App.Api.LatestUpdateAsync(cached: true));
        }
        catch (Exception ex)
        {
            _updateDetail.Text = ex.Message;
            _updateDetail.Foreground = Kit.Brush("DangerBrush");
        }
    }

    private async Task CheckUpdatesAsync()
    {
        if (_updateBusy) return;
        _updateBusy = true;
        _updateStatus.Text = "Consultando GitHub...";
        _updateStatus.Foreground = Kit.Brush("MutedBrush");

        try
        {
            ShowUpdate(await App.Api.LatestUpdateAsync(refresh: true));
        }
        catch (Exception ex)
        {
            _updateStatus.Text = ex.Message;
            _updateStatus.Foreground = Kit.Brush("DangerBrush");
        }
        finally
        {
            _updateBusy = false;
        }
    }

    private void ShowUpdate(UpdateInfoDto info)
    {
        if (info.CheckFailed && info.LatestVersion is null)
        {
            _updateDetail.Text = $"{L.T("Versión instalada:")} {info.CurrentVersion} · " +
                                 L.T("No se pudo consultar GitHub.");
            _updateDetail.Foreground = Kit.Brush("WarningBrush");
            _updateStatus.Text = L.T("Use «Buscar actualizaciones ahora» para volver a intentarlo.");
            _updateStatus.Foreground = Kit.Brush("MutedBrush");
            return;
        }

        if (info.LatestVersion is null)
        {
            _updateDetail.Text = $"{L.T("Versión instalada:")} {info.CurrentVersion} · " +
                                 L.T("No hay versiones publicadas todavía.");
            _updateDetail.Foreground = Kit.Brush("MutedBrush");
            _updateStatus.Text = L.T("Publique una versión en GitHub para activar los avisos.");
            _updateStatus.Foreground = Kit.Brush("MutedBrush");
            return;
        }

        _updateDetail.Text = $"{L.T("Versión instalada:")} {info.CurrentVersion} · " +
                             $"{L.T("Última versión:")} {info.LatestVersion}";

        if (!info.IsNewer)
        {
            _updateDetail.Foreground = Kit.Brush("SuccessBrush");
            _updateStatus.Text = L.T("Ya tiene la última versión.");
            _updateStatus.Foreground = Kit.Brush("SuccessBrush");
            return;
        }

        _updateDetail.Foreground = Kit.Brush("AccentBrush");

        if (!info.ReminderEnabled)
        {
            _updateStatus.Text = L.T("Los recordatorios de nuevas versiones están desactivados.");
        }
        else if (info.IsDismissed)
        {
            _updateStatus.Text = L.T("Aviso oculto para esta versión. Se mostrará con la próxima.");
        }
        else if (info.IsReminderDue)
        {
            _updateStatus.Text = L.T("El aviso se muestra al abrir la consola.");
        }
        else if (info.ReminderAtUtc is DateTime reminderAt)
        {
            _updateStatus.Text = $"{L.T("El aviso aparecerá el")} " +
                                 $"{reminderAt.ToLocalTime():dd/MM/yyyy HH:mm}.";
        }
        else
        {
            _updateStatus.Text = L.T("El aviso aparecerá cuando GitHub informe la fecha de publicación.");
        }

        _updateStatus.Foreground = Kit.Brush("MutedBrush");
    }

    // ============================================================
    //  Carga general
    // ============================================================
    public async Task RefreshAsync()
    {
        _businessLoaded = false;

        var settings = await App.Api.SettingsAsync();

        foreach (var (key, box) in _texts)
            box.Text = settings.TryGetValue(key, out var value)
                ? value
                : _textDefaults.GetValueOrDefault(key, string.Empty);

        foreach (var (key, box) in _printers)
            box.Text = settings.TryGetValue(key, out var printer)
                ? printer
                : _printerDefaults.GetValueOrDefault(key, string.Empty);

        foreach (var (key, check) in _checks)
            check.IsChecked = settings.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed)
                ? parsed
                : _checkDefaults.GetValueOrDefault(key, true);

        _smtpPassword.Password = string.Empty;
        _clearSmtpPassword = false;

        var closingFormat = settings.TryGetValue("DailyClosingFormat", out var formatValue)
            ? TicketPrinter.NormalizeClosingFormat(formatValue)
            : "ticket80";
        _closingFormatBox.SelectedIndex = Math.Max(0,
            Array.FindIndex(ClosingFormats, f => f.Value == closingFormat));

        var emailEnabled = settings.TryGetValue("DailyReportEmailEnabled", out var emailRaw)
                           && bool.TryParse(emailRaw, out var emailParsed) && emailParsed;
        _emailStatus.Text = emailEnabled
            ? "El envío automático del cierre diario por correo está activo."
            : "El envío del cierre diario por correo está desactivado.";
        _emailStatus.Foreground = Kit.Brush(emailEnabled ? "SuccessBrush" : "MutedBrush");

        _businessLoaded = true;
        _businessStatus.Text =
            $"Configuración cargada · {settings.Count} claves. Los cambios se guardan con «Aplicar y guardar».";
        _businessStatus.Foreground = Kit.Brush("MutedBrush");
        _saveStatus.Text = "Configuración cargada. Los cambios se guardan con «Aplicar y guardar».";
        _saveStatus.Foreground = Kit.Brush("MutedBrush");

        await LoadRatesAsync();
        await LoadUsersAsync();
        await LoadBackupsAsync();
        await LoadUpdateAsync();
        await LoadWebFilterAsync();
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

        var field = Kit.Field(label, box);
        field.Margin = new Thickness(0, 0, fullWidth ? 0 : 16, 10);
        parent.Children.Add(field);
    }

    private void AddPrinter(Panel parent, string key, string label, string fallback)
    {
        var box = new ComboBox
        {
            IsEditable = true,
            IsTextSearchEnabled = true,
            ItemsSource = TicketPrinter.InstalledPrinters().ToList(),
            Text = fallback,
            ToolTip = "Elija una impresora instalada o escriba su nombre. " +
                      "Si se deja vacío, se usará la impresora predeterminada de Windows."
        };
        _printers[key] = box;
        _printerDefaults[key] = fallback;

        var field = Kit.Field(label, box);
        field.Margin = new Thickness(0, 0, 16, 10);
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

public sealed class WebFilterRow
{
    public WebFilterRow(WebFilterRuleDto rule)
    {
        Source = rule;
        Pattern = rule.Pattern;
        Kind = rule.Kind == WebFilterRuleKind.Domain ? "Sitio / dominio" : "Palabra clave";
        Status = rule.Active ? "Activa" : "Inactiva";
        Notes = string.IsNullOrWhiteSpace(rule.Notes) ? "—" : rule.Notes!;
    }

    public WebFilterRuleDto Source { get; }
    public string Pattern { get; }
    public string Kind { get; }
    public string Status { get; }
    public string Notes { get; }
}

public sealed class WebFilterRuleDialog : FormDialog
{
    private readonly TextBox _pattern;
    private readonly ComboBox _kindBox;
    private readonly TextBox _notes;
    private readonly CheckBox _active;

    public SaveWebFilterRuleRequest? Result { get; private set; }

    public WebFilterRuleDialog(WebFilterRuleDto? rule)
        : base(rule is null ? "Nueva regla de NetFiltroWeb" : $"Editar «{rule.Pattern}»",
            "Escriba el sitio (ej. ejemplo.com) o la palabra que desea bloquear.")
    {
        _pattern = Add(new TextBox { Text = rule?.Pattern ?? string.Empty }, "Sitio o palabra");

        var options = new[]
        {
            new Option<WebFilterRuleKind>(WebFilterRuleKind.Domain, "Sitio / dominio exacto"),
            new Option<WebFilterRuleKind>(WebFilterRuleKind.Keyword, "Palabra clave")
        };
        _kindBox = AddCombo("Tipo de regla", options);
        _kindBox.SelectedItem = options.First(o => o.Value == (rule?.Kind ?? WebFilterRuleKind.Domain));

        _notes = Add(new TextBox { Text = rule?.Notes ?? string.Empty }, "Notas (opcional)");
        _active = AddCheck("Regla activa", rule?.Active ?? true);
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_pattern.Text))
            throw new InvalidOperationException("Indique el sitio o la palabra que desea bloquear.");

        var kind = ((Option<WebFilterRuleKind>)_kindBox.SelectedItem!).Value;
        Result = new SaveWebFilterRuleRequest(
            _pattern.Text.Trim(),
            kind,
            _active.IsChecked == true,
            string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim());

        base.OnAccept();
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

