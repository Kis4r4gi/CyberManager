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

    public const string RequestMagic = "CYBERMANAGER-DISCOVER-1";
    public const string ResponseMagic = "CYBERMANAGER-STATION-1";
    public const string ConfigureMagic = "CYBERMANAGER-CONFIGURE-1";

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
}
