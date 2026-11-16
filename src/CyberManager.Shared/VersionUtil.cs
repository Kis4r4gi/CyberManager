namespace CyberManager.Shared;

/// <summary>Comparación de versiones numéricas (1.2.0 &gt; 1.1.9) usada por el servidor,
/// el agente y la estación para decidir si hay una actualización disponible.</summary>
public static class VersionUtil
{
    /// <summary>Compara dos versiones; devuelve negativo si <paramref name="left"/> es menor.</summary>
    public static int Compare(string? left, string? right)
    {
        static int[] Parse(string? value)
            => (value ?? string.Empty).Split('.', '-', '+')
                .Select(part => int.TryParse(part, out var number) ? number : 0)
                .ToArray();

        var a = Parse(left);
        var b = Parse(right);
        var length = Math.Max(a.Length, b.Length);

        for (var i = 0; i < length; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y) return x.CompareTo(y);
        }

        return 0;
    }

    /// <summary>Indica si la versión del paquete es más reciente que la instalada.</summary>
    public static bool IsNewer(string? candidate, string? installed)
        => Compare(candidate, installed) > 0;
}
