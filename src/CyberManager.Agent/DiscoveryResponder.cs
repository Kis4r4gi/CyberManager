using System.Net;
using System.Net.Sockets;
using CyberManager.Shared;

namespace CyberManager.Agent;

/// <summary>
/// Escucha el descubrimiento del servidor en el puerto UDP 5093. Responde con la
/// identidad del agente y acepta el ID/token que el administrador confirme en la
/// consola. El servicio se reconecta automáticamente al recibir la configuración.
/// </summary>
public sealed class DiscoveryResponder : BackgroundService
{
    private readonly AgentConfigStore _config;
    private readonly ILogger<DiscoveryResponder> _log;

    public DiscoveryResponder(AgentConfigStore config, ILogger<DiscoveryResponder> log)
    {
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        UdpClient udp;
        try
        {
            udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryProtocol.AgentPort));
            udp.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo abrir el puerto UDP {Port} para el descubrimiento",
                DiscoveryProtocol.AgentPort);
            return;
        }

        using (udp)
        {
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
                if (message is null) continue;

                if (message.Magic == DiscoveryProtocol.RequestMagic)
                {
                    await RespondAsync(udp, result.RemoteEndPoint, stoppingToken);
                }
                else if (message.Magic == DiscoveryProtocol.ConfigureMagic)
                {
                    ApplyConfiguration(message);
                }
            }
        }
    }

    private async Task RespondAsync(UdpClient udp, IPEndPoint remote, CancellationToken ct)
    {
        var options = _config.Current;
        var response = new DiscoveryMessage(
            DiscoveryProtocol.ResponseMagic,
            Role: "agent",
            StationId: options.StationId,
            Token: options.Token,
            ServerUrl: options.ServerUrl,
            MachineName: Environment.MachineName,
            IpAddress: NetworkIdentity.LocalIPv4(),
            MacAddress: NetworkIdentity.PrimaryMac(),
            Version: AppInfo.Version,
            SentUtc: DateTime.UtcNow);

        try
        {
            await udp.SendAsync(DiscoveryProtocol.Encode(response), remote, ct);
        }
        catch
        {
            // El servidor reintentará en el próximo escaneo.
        }
    }

    private void ApplyConfiguration(DiscoveryMessage message)
    {
        if (message.StationId <= 0 || string.IsNullOrWhiteSpace(message.Token)) return;

        var current = _config.Current;
        var serverUrl = string.IsNullOrWhiteSpace(message.ServerUrl)
            ? current.ServerUrl
            : message.ServerUrl!;

        _config.Save(message.StationId, message.Token!, serverUrl);
        _log.LogInformation("Configuración recibida del servidor: equipo {StationId}", message.StationId);
    }
}
