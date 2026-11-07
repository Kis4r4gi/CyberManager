using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;

// El servidor de CyberManager se distribuye únicamente para Windows.
#pragma warning disable CA1416

namespace CyberManager.Server;

/// <summary>
/// Solicita permisos de administrador la primera vez que se inicia el servidor y
/// mantiene abierta la regla de Firewall del puerto de la API.
/// </summary>
public static class Elevation
{
    private const string MarkerName = "server-elevated.marker";

    /// <summary>Indica si el proceso actual pertenece al grupo Administradores.</summary>
    public static bool IsAdministrator
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Un servicio o tarea no interactiva no puede mostrar el aviso de UAC.</summary>
    public static bool IsService => !Environment.UserInteractive;

    /// <summary>
    /// Si el servidor se inició sin privilegios y es la primera ejecución, lo relanza
    /// con <c>runas</c>. Devuelve <c>true</c> cuando ya se lanzó otra instancia y la
    /// actual debe terminar.
    /// </summary>
    public static bool TryElevate(string[] args, int port)
    {
        if (IsAdministrator)
        {
            EnsureFirewallRule(port);
            return false;
        }

        if (IsService || !Environment.UserInteractive) return false;
        if (HasFlag(args, "--no-elevate") || HasFlag(args, "--service")
            || HasFlag(args, "--elevated"))
            return false;

        var marker = MarkerPath();
        if (File.Exists(marker)) return false;

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return false;

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            // Al ejecutar con "dotnet run" el proceso es dotnet.exe; hay que indicarle
            // el ensamblado del servidor como primer argumento.
            if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet",
                    StringComparison.OrdinalIgnoreCase))
            {
                var assembly = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrWhiteSpace(assembly)) start.ArgumentList.Add(assembly);
            }

            foreach (var arg in args)
                start.ArgumentList.Add(arg);
            start.ArgumentList.Add("--elevated");

            Process.Start(start);

            // El marcador se escribe solo si UAC aceptó el relanzamiento: así una
            // cancelación no impide volver a solicitarlo en el próximo inicio.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            }
            catch
            {
                // Si ProgramData no es escribible, el servidor elevado sigue funcionando;
                // solo se volverá a pedir UAC en el próximo inicio interactivo.
            }

            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException
                                       or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Puerto configurado por argumentos, variable de entorno o appsettings.json.</summary>
    public static int ResolvePort(string[] args, int fallback = 5080)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--Server:Port=", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(arg["--Server:Port=".Length..], out var inline) && inline > 0)
                return inline;

            if (string.Equals(arg, "--Server:Port", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length && int.TryParse(args[i + 1], out var next) && next > 0)
                return next;
        }

        var environmentPort = Environment.GetEnvironmentVariable("CYBERMANAGER_SERVER_PORT")
                              ?? Environment.GetEnvironmentVariable("Server__Port");
        if (int.TryParse(environmentPort, out var fromEnvironment) && fromEnvironment > 0)
            return fromEnvironment;

        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(file))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.TryGetProperty("Server", out var server)
                    && server.TryGetProperty("Port", out var value)
                    && value.TryGetInt32(out var configured) && configured > 0)
                    return configured;
            }
        }
        catch
        {
            // Una configuración ilegible se corrige al arrancar; aquí solo se usa el valor por defecto.
        }

        return fallback;
    }

    /// <summary>
    /// Abre el puerto TCP del servidor para las estaciones. Se ejecuta solo con
    /// privilegios de administrador y reutiliza la regla si ya existe.
    /// </summary>
    public static void EnsureFirewallRule(int port)
    {
        if (!IsAdministrator || port <= 0) return;

        var name = $"CyberManager Server {port}";

        try
        {
            var check = Run("netsh",
                $"advfirewall firewall show rule name=\"{name}\"");
            if (check.ExitCode == 0 && check.Output.Contains(name, StringComparison.OrdinalIgnoreCase))
                return;

            // Limpia una regla previa con el mismo nombre para evitar duplicados.
            Run("netsh", $"advfirewall firewall delete rule name=\"{name}\"");

            Run("netsh",
                $"advfirewall firewall add rule name=\"{name}\" dir=in action=allow " +
                $"protocol=TCP localport={port} profile=any " +
                "description=\"Acceso de las estaciones al servidor de CyberManager\"");
        }
        catch
        {
            // Sin Firewall no se impide el funcionamiento local del servidor.
        }
    }

    private static bool HasFlag(IEnumerable<string> args, string flag)
        => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string MarkerPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = AppContext.BaseDirectory;
        return Path.Combine(root, "CyberManager", MarkerName);
    }

    private static (int ExitCode, string Output) Run(string file, string arguments)
    {
        var start = new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(start);
        if (process is null) return (-1, string.Empty);

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(8000))
        {
            try { process.Kill(true); } catch { /* el proceso ya terminó */ }
            return (-1, output + error);
        }

        return (process.ExitCode, output + error);
    }
}
