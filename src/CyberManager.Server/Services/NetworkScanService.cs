using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CyberManager.Shared;

namespace CyberManager.Server.Services;

/// <summary>
/// Descubre estaciones en la red local. Envía una petición UDP al broadcast de cada
/// interfaz y, como respaldo, a todas las direcciones del /24 (algunos switches
/// bloquean el broadcast). El cliente y el agente responden por los puertos 5092 y
/// 5093 con su ID, token, IP, MAC y versión.
/// </summary>
public sealed class NetworkScanService
{
    private static readonly TimeSpan ResponseWindow = TimeSpan.FromMilliseconds(2600);

    public async Task<List<DiscoveryMessage>> ScanAsync(CancellationToken ct)
    {
        var request = DiscoveryProtocol.Encode(new DiscoveryMessage(
            DiscoveryProtocol.RequestMagic, SentUtc: DateTime.UtcNow));

        var results = new Dictionary<string, DiscoveryMessage>(StringComparer.OrdinalIgnoreCase);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.EnableBroadcast = true;
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        foreach (var target in BuildTargets())
        {
            foreach (var port in new[] { DiscoveryProtocol.ClientPort, DiscoveryProtocol.AgentPort })
            {
                try
                {
                    await udp.SendAsync(request, new IPEndPoint(target, port), ct);
                }
                catch
                {
                    // Una dirección inalcanzable no debe detener el resto del escaneo.
                }
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ResponseWindow);

        while (!timeout.IsCancellationRequested)
        {
            try
            {
                var response = await udp.ReceiveAsync(timeout.Token);
                var message = DiscoveryProtocol.Decode(response.Buffer);
                if (message is null || message.Magic != DiscoveryProtocol.ResponseMagic) continue;

                var ip = string.IsNullOrWhiteSpace(message.IpAddress)
                    ? response.RemoteEndPoint.Address.ToString()
                    : message.IpAddress!;
                var key = $"{message.Role}|{message.MachineName}|{message.MacAddress}|{ip}";
                results[key] = message with { IpAddress = ip, SentUtc = DateTime.UtcNow };
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
        }

        return results.Values
            .OrderBy(m => m.MachineName)
            .ThenBy(m => m.Role)
            .ToList();
    }

    /// <summary>Envía el ID y el token definitivos a la estación que respondió al escaneo.</summary>
    public async Task<bool> SendConfigureAsync(string ipAddress, int port,
        DiscoveryMessage configure, CancellationToken ct)
    {
        if (!IPAddress.TryParse(ipAddress, out var address)) return false;

        var bytes = DiscoveryProtocol.Encode(configure);
        using var udp = new UdpClient(AddressFamily.InterNetwork);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await udp.SendAsync(bytes, new IPEndPoint(address, port), ct);
            }
            catch
            {
                return false;
            }

            await Task.Delay(80, ct);
        }

        return true;
    }

    private static IEnumerable<IPAddress> BuildTargets()
    {
        var targets = new HashSet<IPAddress> { IPAddress.Broadcast };

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
                    if (IPAddress.IsLoopback(unicast.Address)) continue;

                    var mask = unicast.IPv4Mask;
                    if (mask is null || mask.Equals(IPAddress.Any)) continue;

                    var ipBytes = unicast.Address.GetAddressBytes();
                    var maskBytes = mask.GetAddressBytes();
                    if (maskBytes.Length != 4) continue;

                    var broadcastBytes = new byte[4];
                    for (var i = 0; i < 4; i++)
                        broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);

                    var broadcast = new IPAddress(broadcastBytes);
                    if (!broadcast.Equals(IPAddress.Broadcast)) targets.Add(broadcast);

                    // Barrido del /24 de la interfaz (máximo 254 hosts).
                    for (var host = 1; host <= 254; host++)
                    {
                        if (host == ipBytes[3]) continue;
                        targets.Add(new IPAddress(new[]
                        {
                            ipBytes[0], ipBytes[1], ipBytes[2], (byte)host
                        }));
                    }
                }
            }
        }
        catch
        {
            // Sin información de interfaces se usa solo el broadcast global.
        }

        return targets;
    }
}
