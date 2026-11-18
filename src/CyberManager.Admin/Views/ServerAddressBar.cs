using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CyberManager.Shared;

namespace CyberManager.Admin.Views;

/// <summary>
/// Muestra en la consola la dirección LAN que deben usar los clientes para
/// encontrar el servidor. La obtiene del propio servidor, así que sigue siendo
/// correcta aunque la consola esté abierta en otro equipo o la IP haya cambiado.
/// </summary>
public sealed class ServerAddressBar : Border
{
    private sealed class HealthInfo
    {
        public string? IpAddress { get; set; }
        public string? ServerUrl { get; set; }
        public string[]? Urls { get; set; }
    }

    private readonly TextBlock _text = Kit.Muted("Consultando la IP del servidor...");

    /// <summary>URL lista para copiar en la configuración del cliente.</summary>
    public string? Address { get; private set; }

    public ServerAddressBar()
    {
        Style = Kit.Style("Card");
        Padding = new Thickness(12, 5, 12, 5);
        Margin = new Thickness(0, 0, 8, 8);
        VerticalAlignment = VerticalAlignment.Center;

        _text.VerticalAlignment = VerticalAlignment.Center;
        _text.ToolTip = "Dirección que deben usar los clientes y el agente para conectarse";

        var copy = Kit.Button("Copiar URL", "GhostButton", CopyAddress);
        copy.VerticalAlignment = VerticalAlignment.Center;
        copy.Padding = new Thickness(8, 2, 8, 2);
        copy.ToolTip = "Copiar la URL del servidor para pegarla en el cliente";

        var content = Kit.HStack(10, Kit.Label("IP del servidor"), _text, copy);
        content.VerticalAlignment = VerticalAlignment.Center;
        Child = content;
    }

    /// <summary>Consulta la IP al servidor; si no responde, usa la configuración local.</summary>
    public async Task RefreshAsync()
    {
        var online = true;
        string? url = null;
        try
        {
            var health = await App.Api.GetAsync<HealthInfo>("api/health");
            url = health.ServerUrl;
            if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(health.IpAddress))
                url = $"http://{health.IpAddress}:5080";
            if (string.IsNullOrWhiteSpace(url)) url = health.Urls?.FirstOrDefault();
        }
        catch
        {
            online = false;
        }

        url = NormalizeFallback(url);
        Address = url;

        var host = HostOf(url);
        _text.Text = string.IsNullOrWhiteSpace(host)
            ? "No disponible"
            : online
                ? $"{host}  (puerto 5080)"
                : $"{host}  (servidor sin conexión)";
        _text.Foreground = new SolidColorBrush(online
            ? Color.FromRgb(148, 163, 184)
            : Color.FromRgb(248, 113, 113));
        ToolTip = string.IsNullOrWhiteSpace(url)
            ? null
            : $"URL del servidor: {url}";
    }

    private void CopyAddress()
    {
        if (string.IsNullOrWhiteSpace(Address)) return;
        Clipboard.SetText(Address);
        Ui.Info("URL del servidor copiada al portapapeles.");
    }

    private static string? NormalizeFallback(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url)) return url.TrimEnd('/');

        // Servidores anteriores no informan de su IP: se deduce de la URL usada
        // por la consola. Si es localhost, se toma la IP LAN de este equipo.
        var configured = App.ServerUrl;
        var host = HostOf(configured);
        if (string.IsNullOrWhiteSpace(host) ||
            host.Equals("localhost", System.StringComparison.OrdinalIgnoreCase) ||
            host == "127.0.0.1" || host == "::1")
        {
            var lan = NetworkIdentity.LocalIPv4Addresses(onlyWithGateway: true).FirstOrDefault()
                      ?? NetworkIdentity.LocalIPv4Addresses().FirstOrDefault();
            if (lan is not null) return $"http://{lan}:5080";
        }

        return configured.TrimEnd('/');
    }

    private static string? HostOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri)) return uri.Host;
        return url.TrimEnd('/');
    }
}
