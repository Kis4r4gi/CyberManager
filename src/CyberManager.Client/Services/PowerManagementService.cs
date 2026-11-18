using System.Runtime.InteropServices;

namespace CyberManager.Client.Services;

/// <summary>
/// Mantiene despierto el equipo mientras el cliente está en ejecución: evita la
/// suspensión, la hibernación y el apagado automático de la pantalla. El bloqueo
/// se aplica al hilo que llama y se mantiene hasta llamar a <see cref="Disable"/>.
/// </summary>
public static class PowerManagementService
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint flags);

    /// <summary>Impide que Windows suspenda el equipo o apague la pantalla por inactividad.</summary>
    public static void Enable()
        => SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired);

    /// <summary>Devuelve el control de la energía a Windows.</summary>
    public static void Disable()
        => SetThreadExecutionState(EsContinuous);
}
