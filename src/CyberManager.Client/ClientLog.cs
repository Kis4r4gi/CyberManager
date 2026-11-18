using System.IO;
using CyberManager.Desktop.Api;

namespace CyberManager.Client;

/// <summary>
/// Registro de diagnóstico del cliente. Se guarda junto a la configuración y se
/// recorta al pasar de 1 MB para no llenar el disco de un equipo que lleva meses
/// encendido. Sirve para saber por qué un equipo no recibe órdenes o no se conecta.
/// </summary>
internal static class ClientLog
{
    private const long MaxBytes = 1_000_000;
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? error = null)
        => Write("ERROR", error is null ? message : $"{message}: {error}");

    private static void Write(string level, string message)
    {
        try
        {
            var file = Path.Combine(AppPaths.DataDir("Client"), "client.log");

            lock (Gate)
            {
                var info = new FileInfo(file);
                if (info.Exists && info.Length > MaxBytes)
                {
                    try { File.Delete(file); } catch { /* se sigue usando */ }
                }

                File.AppendAllText(file,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Un fallo al registrar no debe afectar al equipo.
        }
    }
}
