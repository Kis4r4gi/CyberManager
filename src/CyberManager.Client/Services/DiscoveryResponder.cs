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
    private string[] _serverAddresses = Array.Empty<string>();
    private DateTime _serverAddressesAt = DateTime.MinValue;

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
                ApplyConfiguration(message, result.RemoteEndPoint);
            }
        }
    }

    private async Task RespondAsync(IPEndPoint remote, CancellationToken ct)
    {
        var config = AppConfig.Load("Client");

        // Una estación ya configurada no vuelve a exponer su token por UDP: el
        // servidor solo lo necesita la primera vez, cuando aún no hay token.
        var configured = config.StationId > 0 && !string.IsNullOrWhiteSpace(config.StationToken);

        var response = new DiscoveryMessage(
            DiscoveryProtocol.ResponseMagic,
            Role: "client",
            StationId: config.StationId,
            Token: configured ? null : config.StationToken,
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

    private void ApplyConfiguration(DiscoveryMessage message, IPEndPoint remote)
    {
        if (message.StationId <= 0 || string.IsNullOrWhiteSpace(message.Token)) return;

        var config = AppConfig.Load("Client");

        // Una estación ya configurada solo acepta configuración del servidor que
        // tiene guardado. Antes cualquier equipo de la red podía reasignarle el
        // token y el servidor, secuestrando la estación.
        var configured = config.StationId > 0 && !string.IsNullOrWhiteSpace(config.StationToken);
        if (configured && !IsConfiguredServer(remote, config)) return;

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

    /// <summary>Comprueba que el paquete venga de la IP del servidor configurado.</summary>
    private bool IsConfiguredServer(IPEndPoint remote, AppConfig config)
    {
        if (!Uri.TryCreate(config.ServerUrl, UriKind.Absolute, out var uri)) return false;

        var address = remote.Address.ToString();
        if (uri.Host.Equals(address, StringComparison.OrdinalIgnoreCase)) return true;

        if (DateTime.UtcNow - _serverAddressesAt > TimeSpan.FromMinutes(5))
        {
            try
            {
                _serverAddresses = Dns.GetHostAddresses(uri.Host)
                    .Select(a => a.ToString()).ToArray();
            }
            catch
            {
                _serverAddresses = Array.Empty<string>();
            }
            _serverAddressesAt = DateTime.UtcNow;
        }

        return _serverAddresses.Contains(address, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _udp.Dispose();
        try { _loop.Wait(TimeSpan.FromSeconds(1)); } catch { /* cerrando */ }
        _cts.Dispose();
    }
}
