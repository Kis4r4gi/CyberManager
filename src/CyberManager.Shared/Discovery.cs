using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace CyberManager.Shared;

/// <summary>
/// Protocolo UDP con el que el servidor descubre las estaciones instaladas en la red
/// local. La estación responde con su ID, token, IP, MAC y versión; el servidor puede
/// enviarle después un mensaje de configuración con el token que debe usar.
/// </summary>
public static class DiscoveryProtocol
{
    /// <summary>Puerto donde escucha la pantalla del cliente.</summary>
    public const int ClientPort = 5092;

    /// <summary>Puerto donde escucha el agente de Windows.</summary>
    public const int AgentPort = 5093;

    /// <summary>Puerto donde el servidor responde a la b�squeda de la pantalla y la consola.</summary>
    public const int ServerPort = 5094;

    public const string RequestMagic = "CYBERMANAGER-DISCOVER-1";
    public const string ResponseMagic = "CYBERMANAGER-STATION-1";
    public const string ConfigureMagic = "CYBERMANAGER-CONFIGURE-1";

    /// <summary>Pregunta difundida por la pantalla, la consola o el agente: "�d�nde est� el servidor?".</summary>
    public const string FindServerMagic = "CYBERMANAGER-FIND-SERVER-1";

    /// <summary>Respuesta del servidor con su direcci�n actual en la red local.</summary>
    public const string ServerMagic = "CYBERMANAGER-SERVER-1";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static byte[] Encode(DiscoveryMessage message)
        => JsonSerializer.SerializeToUtf8Bytes(message, Json);

    public static DiscoveryMessage? Decode(ReadOnlySpan<byte> data)
    {
        try
        {
            return JsonSerializer.Deserialize<DiscoveryMessage>(data, Json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Mensaje único del protocolo de descubrimiento. El campo <see cref="Magic"/> indica
/// si es una petición, una respuesta de la estación o una configuración enviada por
/// el servidor.
/// </summary>
public sealed record DiscoveryMessage(
    string Magic,
    string? Role = null,
    int StationId = 0,
    string? Token = null,
    string? ServerUrl = null,
    string? MachineName = null,
    string? IpAddress = null,
    string? MacAddress = null,
    string? Version = null,
    DateTime? SentUtc = null);

/// <summary>
/// Identidad que una estación incluye al buscar el servidor. Así el servidor puede
/// ubicarla y registrarla aunque el firewall bloquee el puerto UDP 5092.
/// </summary>
public sealed record DiscoveryIdentity(
    string Role = "client",
    int StationId = 0,
    string? Token = null,
    string? MachineName = null,
    string? IpAddress = null,
    string? MacAddress = null,
    string? Version = null);

/// <summary>Utilidades para obtener la identidad de red del equipo local.</summary>
public static class NetworkIdentity
{
    /// <summary>IPv4 de la interfaz con puerta de enlace; null si no hay red.</summary>
    public static string? LocalIPv4()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(n =>
                {
                    var properties = n.GetIPProperties();
                    return new
                    {
                        Addresses = properties.UnicastAddresses
                            .Select(a => a.Address)
                            .Where(a => a.AddressFamily == AddressFamily.InterNetwork
                                        && !IPAddress.IsLoopback(a)
                                        && !a.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                            .ToList(),
                        HasGateway = properties.GatewayAddresses
                            .Any(g => g.Address is not null && !g.Address.Equals(IPAddress.Any))
                    };
                })
                .Where(c => c.Addresses.Count > 0)
                .ToList();

            var preferred = candidates.FirstOrDefault(c => c.HasGateway) ?? candidates.FirstOrDefault();
            return preferred?.Addresses.FirstOrDefault()?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>MAC de la interfaz principal en formato AA:BB:CC:DD:EE:FF.</summary>
    public static string? PrimaryMac()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                            && n.GetPhysicalAddress().GetAddressBytes().Length == 6)
                .ToList();

            var preferred = interfaces.FirstOrDefault(n =>
                                 n.GetIPProperties().GatewayAddresses.Any(g =>
                                     g.Address is not null && !g.Address.Equals(IPAddress.Any)))
                             ?? interfaces.FirstOrDefault();

            var bytes = preferred?.GetPhysicalAddress().GetAddressBytes();
            return bytes is { Length: 6 }
                ? string.Join(':', bytes.Select(b => b.ToString("X2")))
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Direcciones IPv4 de las interfaces activas. Con <paramref name="onlyWithGateway"/>
    /// se limita a la red que realmente sale a Internet/LAN y se descartan adaptadores
    /// virtuales (Hyper-V, VPN, etc.).
    /// </summary>
    public static IReadOnlyList<string> LocalIPv4Addresses(bool onlyWithGateway = false)
    {
        try
        {
            var result = new List<string>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel) continue;

                var properties = nic.GetIPProperties();
                if (onlyWithGateway && !properties.GatewayAddresses.Any(g =>
                        g.Address is not null && !g.Address.Equals(IPAddress.Any)))
                    continue;

                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(unicast.Address)) continue;
                    var text = unicast.Address.ToString();
                    if (text.StartsWith("169.254.", StringComparison.Ordinal)) continue;
                    if (!result.Contains(text)) result.Add(text);
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Direcciones de difusi�n donde buscar al servidor: la global y la de cada
    /// subred activa. Un router puede filtrar 255.255.255.255, pero casi siempre
    /// deja pasar la difusi�n de la propia subred.
    /// </summary>
    public static IReadOnlyList<IPAddress> BroadcastAddresses()
    {
        var result = new List<IPAddress> { IPAddress.Broadcast };
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel) continue;

                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var mask = unicast.IPv4Mask;
                    if (mask is null) continue;

                    var ipBytes = unicast.Address.GetAddressBytes();
                    var maskBytes = mask.GetAddressBytes();
                    if (ipBytes.Length != 4 || maskBytes.Length != 4) continue;

                    var broadcast = new byte[4];
                    for (var i = 0; i < 4; i++)
                        broadcast[i] = (byte)(ipBytes[i] | ~maskBytes[i]);

                    var address = new IPAddress(broadcast);
                    if (!result.Contains(address)) result.Add(address);
                }
            }
        }
        catch
        {
            // Sin informaci�n de interfaces queda solo la difusi�n global.
        }

        return result;
    }
}
