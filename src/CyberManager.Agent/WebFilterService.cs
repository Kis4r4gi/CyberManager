using System.Net;
using System.Net.Sockets;
using System.Text;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Agent;

/// <summary>
/// Aplica el módulo NetFiltroWeb en el equipo: escribe los dominios prohibidos en
/// el archivo hosts y, si está activado, levanta un proxy local que bloquea los
/// sitios cuyo nombre contenga alguna palabra clave de la lista.
/// </summary>
public sealed class WebFilterService : IDisposable
{
    private const string BlockStart = "# >>> CyberManager NetFiltroWeb >>>";
    private const string BlockEnd = "# <<< CyberManager NetFiltroWeb <<<";

    private readonly ILogger<WebFilterService> _log;
    private readonly object _sync = new();
    private List<WebFilterRuleDto> _rules = new();
    private TcpListener? _proxy;
    private CancellationTokenSource? _proxyCts;
    private int _proxyPort;
    private string _hostsSignature = string.Empty;

    public WebFilterService(ILogger<WebFilterService> log) => _log = log;

    /// <summary>Aplica una lista descargada del servidor.</summary>
    public void Apply(WebFilterPlaylistDto playlist)
    {
        lock (_sync) _rules = playlist.Rules ?? new List<WebFilterRuleDto>();

        ApplyHosts(playlist.Rules ?? new List<WebFilterRuleDto>());
        ApplyProxy(playlist.ProxyEnabled, playlist.ProxyPort);
    }

    /// <summary>Escribe las reglas de dominio en el archivo hosts de Windows.</summary>
    private void ApplyHosts(List<WebFilterRuleDto> rules)
    {
        var domains = rules
            .Where(r => r.Kind == WebFilterRuleKind.Domain)
            .Select(r => Normalize(r.Pattern))
            .Where(d => d.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d)
            .ToList();

        var signature = string.Join('|', domains);
        if (string.Equals(signature, _hostsSignature, StringComparison.Ordinal)) return;

        try
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var hosts = Path.Combine(system, "drivers", "etc", "hosts");
            if (!File.Exists(hosts)) return;

            var existing = File.ReadAllLines(hosts).ToList();
            var cleaned = new List<string>();
            var inside = false;
            foreach (var line in existing)
            {
                if (line.Trim().Equals(BlockStart, StringComparison.Ordinal)) { inside = true; continue; }
                if (line.Trim().Equals(BlockEnd, StringComparison.Ordinal)) { inside = false; continue; }
                if (!inside) cleaned.Add(line);
            }

            if (domains.Count > 0)
            {
                cleaned.Add(BlockStart);
                foreach (var domain in domains)
                {
                    cleaned.Add($"127.0.0.1 {domain}");
                    cleaned.Add($"127.0.0.1 www.{domain}");
                    cleaned.Add($"::1 {domain}");
                }
                cleaned.Add(BlockEnd);
            }

            File.WriteAllLines(hosts, cleaned, Encoding.UTF8);
            _hostsSignature = signature;
            _log.LogInformation("NetFiltroWeb: {Count} dominio(s) bloqueado(s) en hosts", domains.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "NetFiltroWeb: no se pudo actualizar el archivo hosts");
        }
    }

    private static string Normalize(string value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (text.StartsWith("www.", StringComparison.Ordinal)) text = text[4..];
        return text.Trim('.', ' ');
    }

    private void ApplyProxy(bool enabled, int port)
    {
        port = Math.Clamp(port, 1024, 65535);

        lock (_sync)
        {
            if (!enabled)
            {
                StopProxy();
                return;
            }

            if (_proxy is not null && _proxyPort == port) return;

            StopProxy();
            try
            {
                _proxy = new TcpListener(IPAddress.Loopback, port);
                _proxy.Start();
                _proxyPort = port;
                _proxyCts = new CancellationTokenSource();
                var listener = _proxy;
                var token = _proxyCts.Token;
                _ = Task.Run(() => ProxyLoopAsync(listener, token));
                _log.LogInformation("NetFiltroWeb: proxy local activo en 127.0.0.1:{Port}", port);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "NetFiltroWeb: no se pudo iniciar el proxy local en el puerto {Port}", port);
                _proxy = null;
            }
        }
    }

    private void StopProxy()
    {
        try { _proxyCts?.Cancel(); } catch { /* ya cancelado */ }
        try { _proxy?.Stop(); } catch { /* ya detenido */ }
        _proxyCts?.Dispose();
        _proxyCts = null;
        _proxy = null;
    }

    private async Task ProxyLoopAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { continue; }

            _ = Task.Run(() => HandleClientAsync(client, token), token);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 15000;
                var stream = client.GetStream();
                var headerText = await ReadHeadersAsync(stream, token);
                if (string.IsNullOrWhiteSpace(headerText)) return;

                var lines = headerText.Split("\r\n", StringSplitOptions.None);
                var requestLine = lines[0].Split(' ');
                if (requestLine.Length < 2) return;

                var method = requestLine[0].ToUpperInvariant();
                var target = requestLine[1];

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines.Skip(1))
                {
                    var colon = line.IndexOf(':');
                    if (colon <= 0) continue;
                    headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }

                if (method == "CONNECT")
                {
                    var (host, port) = SplitHostPort(target, 443);
                    if (IsBlocked(host))
                    {
                        await WriteForbiddenAsync(stream, token);
                        return;
                    }

                    await TunnelAsync(client, stream, host, port, token);
                    return;
                }

                var uri = Uri.TryCreate(target, UriKind.Absolute, out var absolute)
                    ? absolute
                    : null;
                var requestHost = uri?.Host ?? headers.GetValueOrDefault("Host", string.Empty);
                requestHost = requestHost.Split(':')[0];

                if (IsBlocked(requestHost))
                {
                    await WriteForbiddenAsync(stream, token);
                    return;
                }

                if (uri is null)
                {
                    await WriteAsync(stream, "HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n", token);
                    return;
                }

                await ForwardHttpAsync(stream, method, uri, headers, token);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
            {
                // El cliente cerró la conexión o el destino no respondió.
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "NetFiltroWeb: error atendiendo una conexión del proxy");
            }
        }
    }

    private bool IsBlocked(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        var name = Normalize(host);

        List<WebFilterRuleDto> rules;
        lock (_sync) rules = _rules;

        foreach (var rule in rules)
        {
            if (!rule.Active) continue;
            var pattern = Normalize(rule.Pattern);
            if (pattern.Length == 0) continue;

            if (rule.Kind == WebFilterRuleKind.Domain)
            {
                if (name.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("." + pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task ForwardHttpAsync(NetworkStream client, string method, Uri uri,
        Dictionary<string, string> headers, CancellationToken token)
    {
        using var upstream = new TcpClient();
        await upstream.ConnectAsync(uri.Host, uri.Port == -1 ? 80 : uri.Port, token);
        var upStream = upstream.GetStream();

        var builder = new StringBuilder();
        builder.Append(method).Append(' ').Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n");
        foreach (var (key, value) in headers)
        {
            if (key.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                continue;
            builder.Append(key).Append(": ").Append(value).Append("\r\n");
        }
        builder.Append("Connection: close\r\n\r\n");

        var data = Encoding.ASCII.GetBytes(builder.ToString());
        await upStream.WriteAsync(data, token);

        await CopyAsync(upStream, client, token);
    }

    private static async Task TunnelAsync(TcpClient client, NetworkStream clientStream,
        string host, int port, CancellationToken token)
    {
        using var upstream = new TcpClient();
        await upstream.ConnectAsync(host, port, token);
        var upStream = upstream.GetStream();

        await WriteAsync(clientStream,
            "HTTP/1.1 200 Connection Established\r\nProxy-Agent: CyberManager NetFiltroWeb\r\n\r\n",
            token);

        var toUpstream = CopyAsync(clientStream, upStream, token);
        var toClient = CopyAsync(upStream, clientStream, token);
        await Task.WhenAny(toUpstream, toClient);
        try { client.Close(); } catch { /* cerrada */ }
        try { upstream.Close(); } catch { /* cerrada */ }
    }

    private static async Task CopyAsync(Stream from, Stream to, CancellationToken token)
    {
        var buffer = new byte[81920];
        try
        {
            int read;
            while ((read = await from.ReadAsync(buffer, token)) > 0)
                await to.WriteAsync(buffer.AsMemory(0, read), token);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Fin normal de la conexión.
        }
    }

    private static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new List<byte>(1024);
        var single = new byte[1];
        while (buffer.Count < 65536)
        {
            var read = await stream.ReadAsync(single, token);
            if (read == 0) break;
            buffer.Add(single[0]);

            var count = buffer.Count;
            if (count >= 4 && buffer[count - 4] == '\r' && buffer[count - 3] == '\n' &&
                buffer[count - 2] == '\r' && buffer[count - 1] == '\n')
                break;
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static async Task WriteForbiddenAsync(NetworkStream stream, CancellationToken token)
    {
        const string body =
            "<html><body style=\"font-family:Segoe UI;background:#0b1526;color:#e8effa;" +
            "text-align:center;padding:60px\"><h2>Acceso bloqueado</h2>" +
            "<p>Este sitio está restringido por NetFiltroWeb.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 403 Forbidden\r\nContent-Type: text/html; charset=utf-8\r\n" +
                     $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await WriteAsync(stream, header, token);
        await stream.WriteAsync(bytes, token);
    }

    private static Task WriteAsync(NetworkStream stream, string text, CancellationToken token)
        => stream.WriteAsync(Encoding.ASCII.GetBytes(text), token).AsTask();

    private static (string Host, int Port) SplitHostPort(string value, int fallbackPort)
    {
        var colon = value.LastIndexOf(':');
        if (colon > 0 && int.TryParse(value[(colon + 1)..], out var port))
            return (value[..colon], port);
        return (value, fallbackPort);
    }

    public void Dispose()
    {
        lock (_sync) StopProxy();
    }
}
