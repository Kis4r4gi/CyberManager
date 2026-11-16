using System.Net;
using System.Net.Sockets;
using CyberManager.Desktop.Api;
using CyberManager.Shared;

namespace CyberManager.Client.Services;

/// <summary>
/// Escucha las peticiones de descubrimiento del servidor en el puerto UDP 5092.
/// Responde con el ID, token, IP, MAC y versión del equipo. Cuando el administrador
/// confirma el escaneo, recibe el token definitivo y actualiza config.json.
/// </summary>
public sealed class DiscoveryResponder : IDisposable
{
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    /// <summary>Se dispara cuando el servidor envía una configuración nueva.</summary>
    public event Action<DiscoveryMessage>? Configured;

    public DiscoveryResponder()
    {
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryProtocol.ClientPort));
        _udp.EnableBroadcast = true;
        _loop = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udp.ReceiveAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                continue;
            }

            var message = DiscoveryProtocol.Decode(result.Buffer);
            if (message is null) continue;

            if (message.Magic == DiscoveryProtocol.RequestMagic)
            {
                await RespondAsync(result.RemoteEndPoint, ct);
            }
            else if (message.Magic == DiscoveryProtocol.ConfigureMagic)
            {
                ApplyConfiguration(message);
            }
        }
    }

    private async Task RespondAsync(IPEndPoint remote, CancellationToken ct)
    {
        var config = AppConfig.Load("Client");
        var response = new DiscoveryMessage(
            DiscoveryProtocol.ResponseMagic,
            Role: "client",
            StationId: config.StationId,
            Token: config.StationToken,
            ServerUrl: config.ServerUrl,
            MachineName: Environment.MachineName,
            IpAddress: NetworkIdentity.LocalIPv4(),
            MacAddress: NetworkIdentity.PrimaryMac(),
            Version: AppInfo.Version,
            SentUtc: DateTime.UtcNow);

        try
        {
            await _udp.SendAsync(DiscoveryProtocol.Encode(response), remote, ct);
        }
        catch
        {
            // Si la respuesta no sale, el servidor volverá a intentar en el próximo escaneo.
        }
    }

    private void ApplyConfiguration(DiscoveryMessage message)
    {
        if (message.StationId <= 0 || string.IsNullOrWhiteSpace(message.Token)) return;

        var config = AppConfig.Load("Client");
        config.ServerUrl = string.IsNullOrWhiteSpace(message.ServerUrl)
            ? config.ServerUrl
            : message.ServerUrl!;
        config.StationId = message.StationId;
        config.StationToken = message.Token!;
        config.StationIpAddress = NetworkIdentity.LocalIPv4();
        config.StationMacAddress = NetworkIdentity.PrimaryMac();
        config.Save("Client");

        Configured?.Invoke(message);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _udp.Dispose();
        try { _loop.Wait(TimeSpan.FromSeconds(1)); } catch { /* cerrando */ }
        _cts.Dispose();
    }
}
