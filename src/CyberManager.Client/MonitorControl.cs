using System.Runtime.InteropServices;

namespace CyberManager.Client;

/// <summary>Apaga y enciende el monitor de la estación.</summary>
public static class MonitorControl
{
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;
    private static readonly IntPtr HwndBroadcast = new(0xFFFF);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static void TurnOff()
        => SendMessage(HwndBroadcast, WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(2));

    public static void TurnOn()
        => SendMessage(HwndBroadcast, WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(-1));
}
