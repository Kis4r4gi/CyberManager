using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CyberManager.Shared;

namespace CyberManager.Server.Services;

/// <summary>
/// Responde por difusi�n UDP a las pantallas, consolas y agentes que buscan el
/// servidor. Es lo que permite que un cambio de IP (reinicio del router o del
/// equipo) no obligue a reconfigurar cada PC de la red.
/// </summary>
public sealed class ServerDiscoveryService : BackgroundService
{
    private readonly ILogger<ServerDiscoveryService> _log;
    private readonly int _port;
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestUtc = new();

    public ServerDiscoveryService(IConfiguration configuration, ILogger<ServerDiscoveryService> log)
    {
        _log = log;
        _port = configuration.GetValue<int?>("Server:Port") ?? 5080;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);

        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryProtocol.ServerPort));
            udp.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            // Sin el puerto UDP el servidor sigue funcionando: las estaciones que ya
            // tienen su IP guardada se conectan igual.
            _log.LogWarning(ex, "No se pudo abrir el descubrimiento UDP {Port}", DiscoveryProtocol.ServerPort);
            return;
        }

        _log.LogInformation("Descubrimiento del servidor activo en UDP {Port}", DiscoveryProtocol.ServerPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(stoppingToken);
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
            if (message is null || message.Magic != DiscoveryProtocol.FindServerMagic) continue;

            // Un registro por IP y como máximo cada 5 minutos sirve para comprobar
            // en los logs si una estación está intentando encontrar el servidor.
            var requester = result.RemoteEndPoint.Address.ToString();
            if (!_lastRequestUtc.TryGetValue(requester, out var last) ||
                DateTime.UtcNow - last > TimeSpan.FromMinutes(5))
            {
                _lastRequestUtc[requester] = DateTime.UtcNow;
                _log.LogInformation("Petición de descubrimiento recibida desde {Requester}", requester);
            }

            // Se responde con todas las direcciones locales utilizables: el equipo
            // puede tener adaptadores virtuales (Hyper-V, WSL, VPN) y la estación debe
            // poder probar la que realmente alcanza.
            foreach (var ip in CandidateAddresses(result.RemoteEndPoint.Address))
            {
                var reply = new DiscoveryMessage(
                    DiscoveryProtocol.ServerMagic,
                    Role: "server",
                    ServerUrl: $"http://{ip}:{_port}",
                    MachineName: Environment.MachineName,
                    IpAddress: ip,
                    Version: AppInfo.Version,
                    SentUtc: DateTime.UtcNow);

                try
                {
                    await udp.SendAsync(DiscoveryProtocol.Encode(reply), result.RemoteEndPoint, stoppingToken);
                }
                catch (SocketException)
                {
                    // Si la respuesta no sale, el solicitante probará su dirección
                    // guardada o recorrerá la subred.
                }
            }
        }
    }

    /// <summary>
    /// Direcciones que se anuncian, en orden de preferencia: la interfaz por la que
    /// se llega al solicitante, las que tienen puerta de enlace y, al final, el resto.
    /// </summary>
    private static List<string> CandidateAddresses(IPAddress remote)
    {
        var result = new List<string>();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!result.Contains(value, StringComparer.OrdinalIgnoreCase)) result.Add(value);
        }

        Add(AddressFor(remote)?.ToString());
        foreach (var address in NetworkIdentity.LocalIPv4Addresses(onlyWithGateway: true)) Add(address);
        foreach (var address in NetworkIdentity.LocalIPv4Addresses()) Add(address);

        // Sin interfaces detectadas se responde con la dirección de origen de la
        // petición para que el cliente al menos tenga un candidato.
        if (result.Count == 0) Add(remote.ToString());
        return result;
    }

    /// <summary>
    /// IP local de la interfaz por la que se llega al solicitante. Así la respuesta
    /// no anuncia una tarjeta virtual (Hyper-V, VPN) que la estación no alcanza.
    /// </summary>
    private static IPAddress? AddressFor(IPAddress remote)
    {
        try
        {
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            probe.Connect(new IPEndPoint(remote, 9));
            return (probe.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch
        {
            return null;
        }
    }
}
