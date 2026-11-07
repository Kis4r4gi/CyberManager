using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace CyberManager.Uninstaller;

/// <summary>
/// Desinstalador independiente de CyberManager. Busca las instalaciones registradas
/// por Inno Setup (servidor y/o estación) y ejecuta sus desinstaladores en modo
/// silencioso. No elimina la base de datos del negocio.
/// </summary>
internal static class Program
{
    private const string DisplayPrefix = "CyberManager";

    private static int Main(string[] args)
    {
        var silent = args.Any(a =>
            a.Equals("/S", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/SILENT", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--silent", StringComparison.OrdinalIgnoreCase));

        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* consola sin soporte */ }
        Console.Title = "CyberManager · Desinstalador";

        Console.WriteLine("CyberManager · Desinstalador");
        Console.WriteLine("===========================");
        Console.WriteLine();

        var installations = FindInstallations();
        if (installations.Count == 0)
        {
            Console.WriteLine("No se encontraron instalaciones de CyberManager en este equipo.");
            Pause(silent);
            return 1;
        }

        Console.WriteLine("Se desinstalarán los siguientes componentes:");
        foreach (var installation in installations)
            Console.WriteLine($"  • {installation.DisplayName}");

        Console.WriteLine();
        Console.WriteLine("Los datos del negocio (base de datos) no se eliminan.");
        Console.WriteLine();

        if (!silent && !Confirm("¿Desea continuar con la desinstalación? (s/N): "))
        {
            Console.WriteLine("Desinstalación cancelada.");
            return 2;
        }

        var failures = 0;
        foreach (var installation in installations)
        {
            Console.WriteLine();
            Console.WriteLine($"==> Desinstalando {installation.DisplayName}...");
            StopProcesses();
            if (RunUninstaller(installation.Command, silent))
            {
                Console.WriteLine("    Completado.");
            }
            else
            {
                failures++;
                Console.WriteLine("    No se pudo completar. Ejecute el desinstalador como administrador.");
            }
        }

        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine("CyberManager se desinstaló correctamente.");
            TryDeleteMarker();
            ScheduleSelfDelete();
        }
        else
        {
            Console.WriteLine($"La desinstalación terminó con {failures} error(es).");
        }

        Pause(silent);
        return failures == 0 ? 0 : 3;
    }

    private static List<Installation> FindInstallations()
    {
        var results = new List<Installation>();
        var views = Environment.Is64BitOperatingSystem
            ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
            : new[] { RegistryView.Registry32 };

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in views)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstall = baseKey.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (uninstall is null) continue;

                    foreach (var name in uninstall.GetSubKeyNames())
                    {
                        using var key = uninstall.OpenSubKey(name);
                        if (key?.GetValue("DisplayName") is not string displayName
                            || string.IsNullOrWhiteSpace(displayName)
                            || !displayName.StartsWith(DisplayPrefix, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var command = key.GetValue("QuietUninstallString") as string
                                      ?? key.GetValue("UninstallString") as string;
                        if (string.IsNullOrWhiteSpace(command)) continue;

                        if (results.Any(r => string.Equals(
                                r.Command, command, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        results.Add(new Installation(displayName.Trim(), command.Trim()));
                    }
                }
                catch
                {
                    // Una vista del registro no disponible no impide continuar.
                }
            }
        }

        return results
            .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Detiene los procesos y servicios de CyberManager antes de ejecutar los
    /// desinstaladores, para que los archivos no estén bloqueados.
    /// </summary>
    private static void StopProcesses()
    {
        Run("schtasks.exe", "/End /TN \"CyberManagerServer\"");
        Run("sc.exe", "stop CyberManagerAgent");

        foreach (var image in new[]
                 {
                     "CyberManager.Server.exe", "CyberManager.exe", "CyberManager.Admin.exe",
                     "CyberManager.Client.exe", "CyberManager.Agent.exe"
                 })
        {
            Run("taskkill.exe", $"/F /IM \"{image}\"");
        }
    }

    private static void Run(string file, string arguments)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = file,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(info);
            process?.WaitForExit(8000);
        }
        catch
        {
            // Si un proceso no existe o el comando no está disponible, se continúa.
        }
    }

    private static bool RunUninstaller(string command, bool silent)
    {
        var (file, arguments) = SplitCommand(command);
        if (!File.Exists(file))
        {
            Console.WriteLine($"    No se encontró el ejecutable: {file}");
            return false;
        }

        var info = new ProcessStartInfo
        {
            FileName = file,
            Arguments = string.IsNullOrWhiteSpace(arguments)
                ? "/SILENT /SUPPRESSMSGBOXES /NORESTART"
                : $"{arguments} /SILENT /SUPPRESSMSGBOXES /NORESTART",
            WorkingDirectory = Path.GetDirectoryName(file) ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = silent
        };

        try
        {
            using var process = Process.Start(info);
            if (process is null) return false;
            if (!process.WaitForExit(300_000))
            {
                try { process.Kill(true); } catch { /* el proceso ya terminó */ }
                return false;
            }
            return process.ExitCode is 0 or 3010;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Error: {ex.Message}");
            return false;
        }
    }

    private static (string File, string Arguments) SplitCommand(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 0)
                return (command[1..end], command[(end + 1)..].Trim());
        }

        var space = command.IndexOf(' ');
        return space < 0
            ? (command, string.Empty)
            : (command[..space], command[(space + 1)..].Trim());
    }

    private static bool Confirm(string prompt)
    {
        Console.Write(prompt);
        var answer = Console.ReadLine();
        return answer?.Trim().ToLowerInvariant() is "s" or "si" or "sí" or "y" or "yes";
    }

    private static void Pause(bool silent)
    {
        if (silent) return;
        Console.WriteLine();
        Console.Write("Presione Enter para cerrar...");
        Console.ReadLine();
    }

    /// <summary>Borra el marcador de elevación para que una reinstalación vuelva a pedir UAC.</summary>
    private static void TryDeleteMarker()
    {
        try
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var marker = Path.Combine(root, "CyberManager", "server-elevated.marker");
            if (File.Exists(marker)) File.Delete(marker);
        }
        catch
        {
            // El marcador restante solo evita el aviso de UAC en el próximo inicio.
        }
    }

    /// <summary>Programa el borrado del propio ejecutable cuando termine el proceso.</summary>
    private static void ScheduleSelfDelete()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path)) return;

            var folder = Path.GetDirectoryName(path);
            var info = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c ping 127.0.0.1 -n 3 > nul & del /f /q \"{path}\" & " +
                            $"rmdir \"{folder}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(info);
        }
        catch
        {
            // Si no se puede borrar, el archivo queda en ProgramData sin afectar al sistema.
        }
    }

    private sealed record Installation(string DisplayName, string Command);
}
