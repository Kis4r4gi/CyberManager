using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CyberManager.Shared;

/// <summary>
/// Instala o repara el servicio del agente desde la estación. El cliente se
/// ejecuta elevado, así que puede usar <c>sc.exe</c> sin intervención del usuario.
/// El paquete se descarga por el mismo canal que la actualización del cliente.
/// </summary>
public static class AgentInstaller
{
    public const string ServiceName = "CyberManagerAgent";
    public const string InstalledFile = "agent.installed";
    private const string ExeName = "CyberManager.Agent.exe";

    /// <summary>Carpeta <c>Agent</c> hermana de la carpeta <c>Client</c>.</summary>
    public static string? ResolveAgentDir(string clientDir)
    {
        var parent = Directory.GetParent(clientDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
        return parent is null ? null : Path.Combine(parent, "Agent");
    }

    /// <summary>
    /// Comprueba si el paquete indicado ya está instalado y el servicio está activo.
    /// Se usa para no reiniciar el agente en cada comprobación de actualización.
    /// </summary>
    public static async Task<bool> IsInstalledAsync(string agentDir, string agentHash)
    {
        if (string.IsNullOrWhiteSpace(agentHash)) return false;
        if (!File.Exists(Path.Combine(agentDir, ExeName))) return false;

        var marker = Path.Combine(agentDir, InstalledFile);
        if (!File.Exists(marker)) return false;
        var lines = File.ReadAllLines(marker);
        var markerHash = lines.Length > 0 ? lines[^1].Trim() : string.Empty;
        if (!string.Equals(markerHash, agentHash.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return await ServiceRunningAsync();
    }

    /// <summary>
    /// Reemplaza los archivos del agente, reescribe su configuración con la
    /// identidad de la estación y deja el servicio registrado y en ejecución.
    /// </summary>
    public static async Task<(bool Ok, string? Error)> InstallAsync(string stagingDir,
        string agentDir, string serverUrl, int stationId, string token, string version,
        string agentHash)
    {
        if (!Directory.Exists(stagingDir))
            return (false, "No se encontró el paquete del agente descargado.");

        var exe = Path.Combine(stagingDir, ExeName);
        if (!File.Exists(exe))
            return (false, $"El paquete del agente no contiene {ExeName}.");

        try
        {
            Directory.CreateDirectory(agentDir);

            if (await IsInstalledAsync(agentDir, agentHash))
                return (true, null);

            var stop = await StopAndDeleteAsync();
            if (!stop.Ok) return stop;

            foreach (var source in Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(stagingDir, source);
                if (relative.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
                    relative.Equals(InstalledFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                var destination = Path.Combine(agentDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                CopyOver(source, destination);
            }

            WriteSettings(agentDir, serverUrl, stationId, token);

            var installedExe = Path.Combine(agentDir, ExeName);
            var create = await CreateServiceAsync(installedExe);
            if (!create.Ok) return create;

            await RunScAsync($"description {ServiceName} " +
                "\"CyberManager - agente de control del equipo (bloqueo, apagado, reinicio, mensajes).\"");
            await RunScAsync($"failure {ServiceName} reset= 86400 " +
                "actions= restart/5000/restart/10000/restart/30000");

            var start = await RunScAsync($"start {ServiceName}");
            if (start.Code != 0 && !await ServiceRunningAsync())
                return (false, $"No se pudo iniciar el servicio: {start.Output.Trim()}");

            var running = false;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (await ServiceRunningAsync())
                {
                    running = true;
                    break;
                }
                await Task.Delay(500);
            }

            if (!running)
                return (false, "El servicio CyberManagerAgent no pasó a estado de ejecución.");

            File.WriteAllText(Path.Combine(agentDir, InstalledFile),
                $"{version}{Environment.NewLine}{agentHash}{Environment.NewLine}");
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static void CopyOver(string source, string destination)
    {
        try
        {
            File.Copy(source, destination, true);
        }
        catch (IOException)
        {
            var backup = destination + ".old";
            ClientUpdateInstaller.TryDelete(backup);
            if (File.Exists(destination)) File.Move(destination, backup, true);
            File.Copy(source, destination, true);
        }
        catch (UnauthorizedAccessException)
        {
            var backup = destination + ".old";
            ClientUpdateInstaller.TryDelete(backup);
            if (File.Exists(destination)) File.Move(destination, backup, true);
            File.Copy(source, destination, true);
        }
    }

    private static void WriteSettings(string agentDir, string serverUrl, int stationId, string token)
    {
        var path = Path.Combine(agentDir, "appsettings.json");
        JsonObject settings;
        try
        {
            settings = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject()
                : new JsonObject();
        }
        catch
        {
            settings = new JsonObject();
        }

        var logging = settings["Logging"]?.AsObject() ?? new JsonObject();
        var level = logging["LogLevel"]?.AsObject() ?? new JsonObject();
        level["Default"] = "Information";
        level["Microsoft.Hosting.Lifetime"] = "Information";
        logging["LogLevel"] = level;
        settings["Logging"] = logging;

        var agent = settings["Agent"]?.AsObject() ?? new JsonObject();
        var url = serverUrl.Trim().TrimEnd('/');
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "http://" + url;
        agent["ServerUrl"] = url;
        agent["StationId"] = stationId;
        agent["Token"] = token ?? string.Empty;
        if (agent["HeartbeatSeconds"] is null) agent["HeartbeatSeconds"] = 10;
        settings["Agent"] = agent;

        File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<(bool Ok, string? Error)> StopAndDeleteAsync()
    {
        var query = await RunScAsync($"query {ServiceName}");

        if (query.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            await RunScAsync($"stop {ServiceName}");
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var state = await RunScAsync($"query {ServiceName}");
                if (!state.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)) break;
                await Task.Delay(500);
            }
        }

        var delete = await RunScAsync($"delete {ServiceName}");
        if (delete.Code != 0 && !delete.Output.Contains("1060", StringComparison.Ordinal))
            return (false, $"No se pudo quitar el servicio anterior: {delete.Output.Trim()}");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var state = await RunScAsync($"query {ServiceName}");
            if (state.Code != 0 || state.Output.Contains("1060", StringComparison.Ordinal)) break;
            await Task.Delay(500);
        }

        return (true, null);
    }

    private static async Task<(bool Ok, string? Error)> CreateServiceAsync(string exe)
    {
        var arguments = $"create {ServiceName} binPath= \"{exe}\" start= auto " +
                        "DisplayName= \"CyberManager Agent\"";

        var create = await RunScAsync(arguments);
        if (create.Code != 0)
        {
            // sc marca el servicio para borrar y a veces tarda un instante en
            // liberar el nombre; se reintenta una vez antes de rendirse.
            await Task.Delay(1500);
            create = await RunScAsync(arguments);
        }

        if (create.Code != 0)
            return (false, $"No se pudo registrar el servicio: {create.Output.Trim()}");

        return (true, null);
    }

    private static async Task<bool> ServiceRunningAsync()
    {
        var result = await RunScAsync($"query {ServiceName}");
        return result.Code == 0 && result.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int Code, string Output)> RunScAsync(string arguments)
    {
        var sc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe");
        var start = new ProcessStartInfo(sc, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(start);
        if (process is null) return (-1, "No se pudo ejecutar sc.exe.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask + await errorTask;
        return (process.ExitCode, output);
    }
}
