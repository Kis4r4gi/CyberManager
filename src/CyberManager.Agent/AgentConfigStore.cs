using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace CyberManager.Agent;

/// <summary>
/// Configuración viva del agente. El descubrimiento de red puede cambiarla en
/// caliente y el trabajador reconecta sin reiniciar el servicio.
/// </summary>
public sealed class AgentConfigStore
{
    private readonly string _settingsPath;
    private readonly object _gate = new();
    private AgentOptions _current;

    public event Action? Changed;

    public AgentConfigStore(IOptions<AgentOptions> options)
    {
        _current = Clone(options.Value);
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public AgentOptions Current
    {
        get
        {
            lock (_gate) return _current;
        }
    }

    /// <summary>Guarda el ID, el token y la URL y avisa al trabajador para reconectar.</summary>
    public void Save(int stationId, string token, string serverUrl)
    {
        lock (_gate)
        {
            _current = new AgentOptions
            {
                ServerUrl = serverUrl.TrimEnd('/'),
                StationId = stationId,
                Token = token,
                HeartbeatSeconds = _current.HeartbeatSeconds
            };

            try
            {
                JsonObject root;
                if (File.Exists(_settingsPath))
                    root = JsonNode.Parse(File.ReadAllText(_settingsPath)) as JsonObject ?? new JsonObject();
                else
                    root = new JsonObject();

                var agent = root["Agent"] as JsonObject ?? new JsonObject();
                agent["ServerUrl"] = _current.ServerUrl;
                agent["StationId"] = _current.StationId;
                agent["Token"] = _current.Token;
                agent["HeartbeatSeconds"] = _current.HeartbeatSeconds;
                root["Agent"] = agent;

                File.WriteAllText(_settingsPath,
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Si no se puede escribir el archivo, la configuración en memoria
                // sigue activa hasta el próximo reinicio del servicio.
            }
        }

        Changed?.Invoke();
    }

    private static AgentOptions Clone(AgentOptions options) => new()
    {
        ServerUrl = options.ServerUrl,
        StationId = options.StationId,
        Token = options.Token,
        HeartbeatSeconds = options.HeartbeatSeconds
    };
}
