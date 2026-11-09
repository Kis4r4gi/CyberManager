using System.Net;
using System.Net.Sockets;

namespace CyberManager.Server.Services;

/// <summary>
/// Envía un paquete mágico Wake on LAN al equipo indicado. El servidor debe
/// estar en la misma red local (o tener una ruta hacia ella) para que la
/// tarjeta de red del equipo lo reciba.
/// </summary>
public static class WakeOnLanService
{
    /// <summary>Convierte una MAC con separadores : - . o espacios en sus 6 bytes.</summary>
    public static byte[]? ParseMac(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var clean = new string(value.Where(Uri.IsHexDigit).ToArray());
        if (clean.Length != 12) return null;

        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
        {
            if (!byte.TryParse(clean.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out bytes[i]))
                return null;
        }

        return bytes;
    }

    /// <summary>Envía el paquete mágico por UDP a la red local.</summary>
    public static void Send(byte[] mac, string? ipAddress = null, int port = 9)
    {
        if (mac.Length != 6)
            throw new ArgumentException("La dirección MAC debe tener 6 bytes.", nameof(mac));

        var packet = new byte[6 + 16 * mac.Length];
        for (var i = 0; i < 6; i++) packet[i] = 0xFF;
        for (var i = 0; i < 16; i++) Buffer.BlockCopy(mac, 0, packet, 6 + i * mac.Length, mac.Length);

        var targets = new HashSet<string> { "255.255.255.255" };

        // Además del broadcast global, se intenta el broadcast de la subred del equipo.
        if (IPAddress.TryParse(ipAddress, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            bytes[3] = 255;
            targets.Add(new IPAddress(bytes).ToString());
        }

        using var client = new UdpClient { EnableBroadcast = true };
        foreach (var target in targets)
        {
            try
            {
                client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse(target), port));
                // Algunas tarjetas escuchan en el puerto 7 además del 9.
                if (port != 7)
                    client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse(target), 7));
            }
            catch (SocketException)
            {
                // Se intenta el siguiente destino; el llamador informa si ninguno funcionó.
            }
        }
    }
}
