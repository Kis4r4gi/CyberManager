using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

    /// <summary>Busca el servidor en la red local usando /api/health (sin autenticación).</summary>
    private async void Discover_Click(object sender, RoutedEventArgs e)
    {
        DiscoverButton.IsEnabled = false;
        ShowStatus(L.T("Buscando el servidor en la red local..."), isError: false);

        try
        {
            var found = await DiscoverServerAsync();
            if (found is null)
            {
                ShowStatus(L.IsEnglish
                    ? "No server was found. Check that the server is running and the firewall allows port 5080."
                    : "No se encontró el servidor. Verifique que esté encendido y que el firewall permita el puerto 5080.",
                    isError: true);
                return;
            }

            ServerUrlBox.Text = found;
            ShowStatus(L.IsEnglish
                ? $"Server found at {found}. Press «Test connection» to confirm."
                : $"Servidor encontrado en {found}. Pulse «Probar conexión» para confirmar.",
                isError: false);
        }
        finally
        {
            DiscoverButton.IsEnabled = true;
        }
    }

    private static async Task<string?> DiscoverServerAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(900) };

        async Task<bool> ProbeAsync(string url)
        {
            try
            {
                using var response = await http.GetAsync($"{url}/api/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        if (await ProbeAsync("http://127.0.0.1:5080"))
            return "http://127.0.0.1:5080";

        var candidates = new List<string>();
        foreach (var address in LocalIPv4Addresses())
        {
            var parts = address.GetAddressBytes();
            for (var host = 1; host <= 254; host++)
            {
                if (host == parts[3]) continue;
                candidates.Add($"http://{parts[0]}.{parts[1]}.{parts[2]}.{host}:5080");
            }
        }

        using var gate = new SemaphoreSlim(48);
        var probes = candidates.Select(async url =>
        {
            await gate.WaitAsync();
            try
            {
                return await ProbeAsync(url) ? url : null;
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(probes);
        return results.FirstOrDefault(result => result is not null);
    }

    private static IEnumerable<IPAddress> LocalIPv4Addresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(a))
                .Distinct()
                .ToList();
        }
        catch
        {
            return Array.Empty<IPAddress>();
        }
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
