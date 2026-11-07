using System.Diagnostics;
using System.Reflection;

namespace CyberManager.Server;

/// <summary>
/// Lanzador único de CyberManager. Cuando se inicia con <c>--launch-console</c>,
/// deja el servidor ejecutándose en segundo plano y abre la consola de administración.
/// </summary>
public static class Launcher
{
    public const string ConsoleFlag = "--launch-console";

    private static readonly string[] InternalFlags =
    {
        ConsoleFlag, "--elevated", "--no-elevate", "--service"
    };

    public static bool HasConsoleFlag(string[] args)
        => args.Any(a => string.Equals(a, ConsoleFlag, StringComparison.OrdinalIgnoreCase));

    public static bool IsInternalFlag(string arg)
        => InternalFlags.Any(flag => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

    public static string[] RemoveFlag(string[] args, string flag)
        => args.Where(a => !string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>
    /// Inicia el servidor si no está activo, espera a que responda y abre la consola.
    /// Devuelve siempre <c>true</c>: el proceso lanzador termina después.
    /// </summary>
    public static bool RunConsoleLauncher(string[] args, int port)
    {
        var serverArgs = RemoveFlag(args, ConsoleFlag);

        if (!IsServerRunning(port))
        {
            StartServer(serverArgs);
            WaitForServer(port, TimeSpan.FromSeconds(30));
        }

        OpenConsole();
        return true;
    }

    public static bool IsServerRunning(int port)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
            using var response = client
                .GetAsync($"http://127.0.0.1:{port}/api/health")
                .GetAwaiter()
                .GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static void WaitForServer(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsServerRunning(port)) return;
            Thread.Sleep(300);
        }
    }

    public static void OpenConsole()
    {
        var admin = ResolveAdminExecutable();
        if (admin is null) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = admin,
                WorkingDirectory = Path.GetDirectoryName(admin)!,
                UseShellExecute = true
            });
        }
        catch
        {
            // Si la consola no está instalada, el servidor sigue funcionando.
        }
    }

    private static void StartServer(string[] args)
    {
        var executable = ResolveServerExecutable();
        if (executable is null) return;

        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        // Con "dotnet run" el proceso actual es dotnet.exe; el servidor se inicia
        // indicando el ensamblado como primer argumento.
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            var assembly = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrWhiteSpace(assembly)) start.ArgumentList.Add(assembly);
        }

        foreach (var arg in args)
        {
            if (string.Equals(arg, ConsoleFlag, StringComparison.OrdinalIgnoreCase)) continue;
            start.ArgumentList.Add(arg);
        }

        try { Process.Start(start); }
        catch { /* el servidor mostrará el error en su propio proceso */ }
    }

    private static string? ResolveServerExecutable()
    {
        var directory = AppContext.BaseDirectory;

        // El instalador copia el apphost como CyberManager.exe; el servidor real
        // conserva su nombre original para poder iniciarse en segundo plano.
        var original = Path.Combine(directory, "CyberManager.Server.exe");
        if (File.Exists(original)) return original;

        return Environment.ProcessPath;
    }

    private static string? ResolveAdminExecutable()
    {
        var directory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(directory, "..", "Admin", "CyberManager.Admin.exe")),
            Path.GetFullPath(Path.Combine(directory, "..", "..", "..", "..",
                "CyberManager.Admin", "bin", "Debug", "net8.0-windows", "CyberManager.Admin.exe")),
            Path.GetFullPath(Path.Combine(directory, "..", "..", "..", "..",
                "CyberManager.Admin", "bin", "Release", "net8.0-windows", "CyberManager.Admin.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
