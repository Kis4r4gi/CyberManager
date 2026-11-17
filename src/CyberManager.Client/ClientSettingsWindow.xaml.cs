using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

/// <summary>
/// Configuración del cliente para administradores. Los valores se leen y se guardan
/// en el servidor (<c>/api/settings</c>) con el token del administrador; el idioma
/// se conserva solo en este equipo.
/// </summary>
public partial class ClientSettingsWindow : Window
{
    private readonly CyberApi _api;
    private bool _busy;

    /// <summary>Indica que los cambios se guardaron correctamente.</summary>
    public bool Saved { get; private set; }

    /// <summary>El administrador pidió cerrar el cliente y desinstalarlo del equipo.</summary>
    public bool UninstallRequested { get; private set; }

    private sealed class Option
    {
        public string Value { get; }
        public string Text { get; }

        public Option(string value, string text)
        {
            Value = value;
            Text = text;
        }

        public override string ToString() => Text;
    }

    public ClientSettingsWindow(CyberApi api)
    {
        _api = api;
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            Loc.Apply(this);
            BuildCombos();
            await LoadAsync();
        };
    }

    private void BuildCombos()
    {
        WallpaperModeBox.ItemsSource = new List<Option>
        {
            new(nameof(WallpaperMode.None), L.T("Sin fondos")),
            new(nameof(WallpaperMode.Single), L.T("Fondo fijo")),
            new(nameof(WallpaperMode.Slideshow), L.T("Presentación rotativa"))
        };
        WallpaperModeBox.SelectedIndex = 0;

        LanguageBox.ItemsSource = new List<Option>
        {
            new("es", "Español"),
            new("en", "English")
        };
        LanguageBox.SelectedIndex = L.IsEnglish ? 1 : 0;
    }

    private async Task LoadAsync()
    {
        SetStatus(L.T("Cargando la configuración del servidor..."));

        try
        {
            var settings = await _api.SettingsAsync();
            string Get(string key) => settings.TryGetValue(key, out var value) ? value : string.Empty;

            BusinessNameBox.Text = Get("BusinessName");
            BusinessTaxIdBox.Text = Get("BusinessTaxId");
            BusinessPhoneBox.Text = Get("BusinessPhone");
            BusinessAddressBox.Text = Get("BusinessAddress");
            LocalCurrencySymbolBox.Text = Get("LocalCurrencySymbol");
            TicketFooterBox.Text = Get("TicketFooter");
            SessionBarDescriptionBox.Text = Get("SessionBarDescription");
            SessionBarScaleBox.Text = string.IsNullOrWhiteSpace(Get("SessionBarScale"))
                ? "100" : Get("SessionBarScale");
            WallpaperRotationBox.Text = string.IsNullOrWhiteSpace(Get("WallpaperRotationSeconds"))
                ? WallpaperSettings.DefaultRotationSeconds.ToString(CultureInfo.InvariantCulture)
                : Get("WallpaperRotationSeconds");
            WarningMinutesBox.Text = string.IsNullOrWhiteSpace(Get("WarningMinutes")) ? "5" : Get("WarningMinutes");
            GraceMinutesBox.Text = string.IsNullOrWhiteSpace(Get("GraceMinutes")) ? "0" : Get("GraceMinutes");
            AllowPostpaidCheck.IsChecked = GetBool(Get("AllowPostpaid"), true);
            AutoLockOvertimeCheck.IsChecked = GetBool(Get("AutoLockOvertime"), true);

            var mode = WallpaperSettings.ParseMode(Get("WallpaperMode"));
            WallpaperModeBox.SelectedItem = WallpaperModeBox.Items.Cast<Option>()
                .FirstOrDefault(o => o.Value == mode.ToString()) ?? WallpaperModeBox.Items[0];

            LanguageBox.SelectedIndex = L.IsEnglish ? 1 : 0;

            SetStatus(L.T("Revisa los datos y pulsa «Guardar cambios»."));
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
            SaveButton.IsEnabled = false;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        try
        {
            var scale = ParseInt(SessionBarScaleBox.Text, "La escala de la barra", 70, 160);
            var rotation = ParseInt(WallpaperRotationBox.Text, "Los segundos entre fondos",
                WallpaperSettings.MinRotationSeconds, WallpaperSettings.MaxRotationSeconds);
            var warning = ParseInt(WarningMinutesBox.Text, "Los minutos de aviso", 0, 120);
            var grace = ParseInt(GraceMinutesBox.Text, "Los minutos de gracia", 0, 120);

            var mode = (WallpaperModeBox.SelectedItem as Option)?.Value ?? nameof(WallpaperMode.None);
            var language = (LanguageBox.SelectedItem as Option)?.Value ?? "es";

            var values = new Dictionary<string, string>
            {
                ["BusinessName"] = BusinessNameBox.Text.Trim(),
                ["BusinessTaxId"] = BusinessTaxIdBox.Text.Trim(),
                ["BusinessPhone"] = BusinessPhoneBox.Text.Trim(),
                ["BusinessAddress"] = BusinessAddressBox.Text.Trim(),
                ["LocalCurrencySymbol"] = LocalCurrencySymbolBox.Text.Trim(),
                ["TicketFooter"] = TicketFooterBox.Text.Trim(),
                ["SessionBarDescription"] = SessionBarDescriptionBox.Text.Trim(),
                ["SessionBarScale"] = scale.ToString(CultureInfo.InvariantCulture),
                ["WallpaperMode"] = mode,
                ["WallpaperRotationSeconds"] = rotation.ToString(CultureInfo.InvariantCulture),
                ["WarningMinutes"] = warning.ToString(CultureInfo.InvariantCulture),
                ["GraceMinutes"] = grace.ToString(CultureInfo.InvariantCulture),
                ["AllowPostpaid"] = (AllowPostpaidCheck.IsChecked == true).ToString(),
                ["AutoLockOvertime"] = (AutoLockOvertimeCheck.IsChecked == true).ToString()
            };

            _busy = true;
            SaveButton.IsEnabled = false;
            SetStatus(L.T("Guardando los cambios..."));

            await _api.SaveSettingsAsync(values);

            App.Config.Language = language;
            App.Config.Save("Client");
            L.SetLanguage(language);

            Saved = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            _busy = false;
            SaveButton.IsEnabled = true;
        }
    }

    private static int ParseInt(string text, string label, int min, int max)
    {
        if (!int.TryParse((text ?? string.Empty).Trim(), out var value))
            throw new InvalidOperationException($"{label} debe ser un número entero.");
        if (value < min || value > max)
            throw new InvalidOperationException($"{label} debe estar entre {min} y {max}.");
        return value;
    }

    private static bool GetBool(string text, bool fallback)
        => bool.TryParse(text, out var value) ? value : fallback;

    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "MutedBrush");
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Cierra el cliente y lanza el desinstalador de CyberManager con permisos de
    /// administrador. La base de datos del negocio no se elimina.
    /// </summary>
    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var uninstaller = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CyberManager", "CyberManager.Uninstaller.exe");

        if (!File.Exists(uninstaller))
        {
            SetStatus(L.T("No se encontró el desinstalador de CyberManager en este equipo."), true);
            return;
        }

        var confirm = MessageBox.Show(this,
            L.T("Se cerrará el cliente y se quitará CyberManager de este equipo.") + "\n\n" +
            L.T("La base de datos del negocio y la configuración se conservan. ¿Desea continuar?"),
            L.T("Desinstalar CyberManager"), MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            Process.Start(new ProcessStartInfo(uninstaller)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(uninstaller) ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            // El usuario canceló el UAC o no se pudo iniciar el desinstalador.
            SetStatus(ex.Message, true);
            return;
        }

        UninstallRequested = true;
        DialogResult = true;
        Close();
    }
}
