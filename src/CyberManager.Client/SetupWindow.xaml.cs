using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;
using CyberManager.Shared;

namespace CyberManager.Client;

public partial class SetupWindow : Window
{
    private bool _loading;

    public SetupWindow()
    {
        _loading = true;
        InitializeComponent();

        var config = AppConfig.Load("Client");
        L.SetLanguage(config.Language);

        ServerUrlBox.Text = config.ServerUrl;
        StationIdBox.Text = config.StationId > 0 ? config.StationId.ToString() : string.Empty;
        TokenBox.Text = config.StationToken;

        foreach (ComboBoxItem item in LanguageBox.Items)
        {
            if (string.Equals(item.Tag as string, L.Code, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedItem = item;
                break;
            }
        }
        LanguageBox.SelectedItem ??= LanguageBox.Items[0];

        _loading = false;
        Loaded += (_, _) => Loc.Apply(this);
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageBox.SelectedItem is not ComboBoxItem item) return;

        L.SetLanguage(item.Tag as string);
        Loc.Apply(this);
    }

    private bool TryRead(out int stationId)
    {
        stationId = 0;

        if (!int.TryParse(StationIdBox.Text.Trim(), out stationId) || stationId <= 0)
        {
            ShowStatus(L.T("El número de equipo debe ser un número mayor que cero."), isError: true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(TokenBox.Text))
        {
            ShowStatus(L.T("Debe indicar el token del equipo."), isError: true);
            return false;
        }

        return true;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var stationId)) return;

        TestButton.IsEnabled = false;
        ShowStatus(L.T("Conectando con el servidor..."), isError: false);

        try
        {
            var api = new StationApi(ServerUrlBox.Text, stationId, TokenBox.Text.Trim());
            var state = await api.StateAsync();

            var session = state.Session is null
                ? L.T("sin sesión activa")
                : string.Format(L.IsEnglish ? "session #{0} active" : "sesión #{0} activa", state.Session.Id);
            var success = L.IsEnglish
                ? $"Connection successful with {state.Name} ({session})."
                : $"Conexión correcta con {state.Name} ({session}).";
            ShowStatus(success, isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus(L.IsEnglish
                ? $"Could not connect: {ex.Message}"
                : $"No se pudo conectar: {ex.Message}", isError: true);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var stationId)) return;

        var config = new AppConfig
        {
            ServerUrl = ApiClient.Normalize(ServerUrlBox.Text),
            StationId = stationId,
            StationToken = TokenBox.Text.Trim(),
            Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? L.Code
        };
        config.Save("Client");

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "SuccessBrush");
    }
}
