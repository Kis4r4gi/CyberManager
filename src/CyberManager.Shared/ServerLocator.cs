using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace CyberManager.Shared;

/// <summary>
/// Busca el servidor de CyberManager en la red local. Primero prueba la direcci�n
/// guardada y el propio equipo; si no responden, pregunta por difusi�n UDP
/// (puerto <see cref="DiscoveryProtocol.ServerPort"/>) y, como �ltimo recurso,
/// recorre las direcciones de la subred probando <c>/api/health</c>.
/// As� el cliente, la consola y el agente siguen funcionando cuando el servidor
/// cambia de IP por un reinicio del router o del equipo.
/// </summary>
public static class ServerLocator
{
    private const int DefaultPort = 5080;

    private static readonly HttpClient ProbeClient = new() { Timeout = TimeSpan.FromSeconds(4) };

    /// <summary>Normaliza una direcci�n igual que <c>ApiClient.Normalize</c>.</summary>
    public static string Normalize(string? url)
    {
        var text = (url ?? string.Empty).Trim();
        if (text.Length == 0) text = $"http://127.0.0.1:{DefaultPort}";
        if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase)) text = "http://" + text;

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            var isHttps = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!isHttps && uri.IsDefaultPort)
                text = new UriBuilder(uri) { Port = DefaultPort }.Uri.ToString();
        }

        return text.TrimEnd('/') + "/";
    }

    /// <summary>
    /// Direcciones candidatas, en orden de preferencia: la guardada, el propio
    /// equipo, las respuestas UDP y, si nada respondi�, el recorrido de la subred.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindCandidatesAsync(
        string? preferredUrl = null, TimeSpan? udpTimeout = null, CancellationToken ct = default,
        DiscoveryIdentity? identity = null)
    {
        var list = new List<string>();
        AddUnique(list, preferredUrl);
        AddUnique(list, $"http://127.0.0.1:{DefaultPort}");

        foreach (var url in await DiscoverUdpAsync(udpTimeout ?? TimeSpan.FromSeconds(2), ct, identity))
            AddUnique(list, url);

        // Solo se recorre la red si ninguna direcci�n conocida responde.
        var anyKnown = false;
        foreach (var url in list.ToList())
        {
            if (!await ProbeAsync(url, 1200, ct)) continue;
            anyKnown = true;
            break;
        }

        if (!anyKnown)
        {
            foreach (var url in await SweepAsync(ct))
                AddUnique(list, url);
        }

        return list;
    }

    /// <summary>Primera direcci�n que responde como servidor de CyberManager.</summary>
    public static async Task<string?> FindWorkingAsync(
        string? preferredUrl = null, TimeSpan? udpTimeout = null, CancellationToken ct = default,
        DiscoveryIdentity? identity = null)
    {
        foreach (var url in await FindCandidatesAsync(preferredUrl, udpTimeout, ct, identity))
        {
            if (await ProbeAsync(url, 2500, ct)) return url;
        }

        return null;
    }

    /// <summary>Comprueba que la dirección responde <c>/api/health</c> como CyberManager.</summary>
    public static async Task<bool> ProbeAsync(string url, int timeoutMs = 3000, CancellationToken ct = default)
    {
        try
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wait.CancelAfter(Math.Max(250, timeoutMs));

            using var response = await ProbeClient.GetAsync($"{Normalize(url)}api/health", wait.Token);
            if (!response.IsSuccessStatusCode) return false;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(wait.Token));
            var root = document.RootElement;

            if (root.TryGetProperty("status", out var status) &&
                !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
                return false;

            // Los servidores nuevos se identifican; los antiguos solo devuelven status.
            if (root.TryGetProperty("app", out var app) &&
                !string.Equals(app.GetString(), "CyberManager", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Distingue un fallo de red (servidor apagado, IP cambiada, cable desconectado)
    /// de un rechazo del servidor. Un 401 del servidor nunca es un fallo de red.
    /// </summary>
    public static bool IsConnectionError(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException or TimeoutException
            or SocketException)
            return true;

        return ex.InnerException is not null && IsConnectionError(ex.InnerException);
    }

    private static async Task<IReadOnlyList<string>> DiscoverUdpAsync(TimeSpan timeout,
        CancellationToken ct, DiscoveryIdentity? identity)
    {
        var found = new List<string>();

        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            var request = DiscoveryProtocol.Encode(new DiscoveryMessage(
                DiscoveryProtocol.FindServerMagic,
                Role: identity?.Role ?? "client",
                StationId: identity?.StationId ?? 0,
                Token: identity?.Token,
                MachineName: identity?.MachineName,
                IpAddress: identity?.IpAddress ?? NetworkIdentity.LocalIPv4(),
                MacAddress: identity?.MacAddress ?? NetworkIdentity.PrimaryMac(),
                Version: identity?.Version,
                SentUtc: DateTime.UtcNow));

            foreach (var address in NetworkIdentity.BroadcastAddresses())
            {
                try
                {
                    await udp.SendAsync(request, new IPEndPoint(address, DiscoveryProtocol.ServerPort), ct);
                }
                catch (SocketException)
                {
                    // Algunos adaptadores no permiten difundir; se prueban los dem�s.
                }
            }

            var until = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < until)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                wait.CancelAfter(until - DateTime.UtcNow);

                UdpReceiveResult result;
                try
                {
                    result = await udp.ReceiveAsync(wait.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    continue;
                }

                var message = DiscoveryProtocol.Decode(result.Buffer);
                if (message is null || message.Magic != DiscoveryProtocol.ServerMagic) continue;

                var url = string.IsNullOrWhiteSpace(message.ServerUrl)
                    ? $"http://{result.RemoteEndPoint.Address}:{DefaultPort}"
                    : message.ServerUrl!;
                AddUnique(found, url);
            }
        }
        catch
        {
            // Sin difusi�n disponible: queda el recorrido de la subred.
        }

        return found;
    }

    private static async Task<IReadOnlyList<string>> SweepAsync(CancellationToken ct)
    {
        var addresses = NetworkIdentity.LocalIPv4Addresses(onlyWithGateway: true);
        if (addresses.Count == 0) addresses = NetworkIdentity.LocalIPv4Addresses();

        var candidates = new List<string>();
        foreach (var text in addresses)
        {
            if (!IPAddress.TryParse(text, out var ip)) continue;
            var bytes = ip.GetAddressBytes();
            for (var host = 1; host <= 254; host++)
            {
                if (host == bytes[3]) continue;
                candidates.Add($"http://{bytes[0]}.{bytes[1]}.{bytes[2]}.{host}:{DefaultPort}");
            }
        }

        var found = new List<string>();
        using var gate = new SemaphoreSlim(64);
        var tasks = candidates.Select(async url =>
        {
            try
            {
                await gate.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            try
            {
                return await ProbeAsync(url, 800, ct) ? url : null;
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        foreach (var result in await Task.WhenAll(tasks))
        {
            if (result is not null) AddUnique(found, result);
        }

        return found;
    }

    private static void AddUnique(List<string> list, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var normalized = Normalize(url);
        if (list.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return;
        list.Add(normalized);
    }
}
