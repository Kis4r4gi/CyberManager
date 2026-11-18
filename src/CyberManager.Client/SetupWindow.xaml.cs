using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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

        // IP y MAC se autodetectan del adaptador activo si el equipo aún no las tiene.
        IpAddressBox.Text = string.IsNullOrWhiteSpace(config.StationIpAddress)
            ? LocalIpv4() ?? string.Empty
            : config.StationIpAddress!;
        MacAddressBox.Text = string.IsNullOrWhiteSpace(config.StationMacAddress)
            ? LocalMac() ?? string.Empty
            : config.StationMacAddress!;

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

    private bool TryRead(out int stationId, out string? stationToken,
        out string? stationIpAddress, out string? stationMacAddress)
    {
        stationId = 0;
        stationToken = null;
        stationIpAddress = null;
        stationMacAddress = null;

        if (!int.TryParse(StationIdBox.Text.Trim(), out stationId) || stationId <= 0)
        {
            ShowStatus(L.T("El número de equipo debe ser un número mayor que cero."), isError: true);
            return false;
        }

        stationToken = string.IsNullOrWhiteSpace(TokenBox.Text) ? null : TokenBox.Text.Trim();

        // Se acepta con dos puntos, guiones, puntos o espacios: cada adaptador de
        // Windows reporta un formato distinto (este equipo usa guiones).
        var ipText = IpAddressBox.Text.Trim();
        if (ipText.Length > 0 && !IPAddress.TryParse(ipText, out _))
        {
            ShowStatus(L.T("La dirección IP no tiene un formato válido."), isError: true);
            return false;
        }
        stationIpAddress = ipText.Length > 0 ? ipText : null;

        var macText = MacAddressBox.Text.Trim();
        if (macText.Length > 0)
        {
            var hex = new string(macText.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (hex.Length != 12)
            {
                ShowStatus(L.T("La dirección MAC no tiene un formato válido."), isError: true);
                return false;
            }

            // Se guarda siempre normalizada: AA:BB:CC:DD:EE:FF.
            stationMacAddress = string.Join(":",
                Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
        }

        return true;
    }

    /// <summary>IP local del adaptador activo, para no escribirla a mano.</summary>
    private static string? LocalIpv4()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>MAC del adaptador activo, normalizada con dos puntos.</summary>
    private static string? LocalMac()
    {
        try
        {
            var bytes = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                             n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                .Select(n => n.GetPhysicalAddress().GetAddressBytes())
                .FirstOrDefault(b => b.Length == 6);

            return bytes is { Length: 6 }
                ? string.Join(":", bytes.Select(b => b.ToString("X2")))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var stationId, out var stationToken,
                out var stationIpAddress, out var stationMacAddress)) return;

        TestButton.IsEnabled = false;
        ShowStatus(L.T("Conectando con el servidor..."), isError: false);

        try
        {
            // El token es la credencial principal; IP/MAC se usan como respaldo.
            var api = new StationApi(ServerUrlBox.Text, stationId,
                stationIpAddress ?? string.Empty, stationMacAddress ?? string.Empty, stationToken);
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
        if (!TryRead(out var stationId, out var stationToken,
                out var stationIpAddress, out var stationMacAddress)) return;

        var config = new AppConfig
        {
            ServerUrl = ApiClient.Normalize(ServerUrlBox.Text),
            StationId = stationId,
            StationToken = stationToken ?? string.Empty,
            StationIpAddress = stationIpAddress,
            StationMacAddress = stationMacAddress,
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
