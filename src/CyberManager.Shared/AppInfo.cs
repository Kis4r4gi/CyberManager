using System.Reflection;

namespace CyberManager.Shared;

/// <summary>Versión de compilación que comparten el servidor, la consola y el cliente.</summary>
public static class AppInfo
{
    public static string Version { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = typeof(AppInfo).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // El SDK agrega "+<hash>" en compilaciones con control de código fuente.
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
