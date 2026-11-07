using System.Windows;
using CyberManager.Admin.Services;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin;

public partial class App : Application
{
    public static CyberApi Api { get; private set; } = null!;
    public static AdminRealtime Realtime { get; private set; } = null!;
    public static LoginResponse Session { get; private set; } = null!;
    public static string ServerUrl { get; private set; } = "http://127.0.0.1:5080";

    public static event Action<string>? NavigationRequested;

    /// <summary>La aplicación ya está cerrando; no se debe pedir confirmación otra vez.</summary>
    public static bool IsShuttingDown { get; set; }

    /// <summary>Windows está cerrando la sesión o apagando el equipo.</summary>
    public static bool IsSessionEnding { get; private set; }

    public static void RequestNavigation(string key) => NavigationRequested?.Invoke(key);

    /// <summary>Cierra la consola sin volver a mostrar la confirmación de salida.</summary>
    public static void ShutdownApplication()
    {
        IsShuttingDown = true;
        Current.Shutdown();
    }

    public static void SetSession(CyberApi api, LoginResponse session, string serverUrl)
    {
        Api = api;
        Session = session;
        ServerUrl = serverUrl;
    }

    public static async Task InitializeRealtimeAsync()
    {
        if (Realtime is not null) return;
        Realtime = new AdminRealtime();
        try
        {
            await Realtime.ConnectAsync(ServerUrl, Api.Token!);
        }
        catch
        {
            // El tablero seguirá funcionando con sondeo periódico.
        }
    }

    public static async Task DisposeRealtimeAsync()
    {
        if (Realtime is null) return;
        await Realtime.DisposeAsync();
        Realtime = null!;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Windows puede cerrar la sesión o apagar el equipo: en ese caso no se
        // muestra la confirmación de salida.
        SessionEnding += (_, _) => IsSessionEnding = true;

        // Aplica la apariencia guardada antes de mostrar cualquier ventana.
        AppearanceService.Initialize();

        var login = new LoginWindow();
        if (login.ShowDialog() != true)
        {
            ShutdownApplication();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
