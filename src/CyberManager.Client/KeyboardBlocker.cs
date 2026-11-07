using System.Runtime.InteropServices;

namespace CyberManager.Client;

/// <summary>
/// Bloquea los atajos de Windows mientras la pantalla de bloqueo está visible
/// (Alt+Tab, tecla Windows, Ctrl+Esc, Alt+F4, etc.).
/// Ctrl+Shift+F12 se deja pasar para el acceso técnico a la configuración.
/// </summary>
public sealed class KeyboardBlocker : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F4 = 0x73;
    private const int VK_F12 = 0x7B;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;

    public KeyboardBlocker()
    {
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    private static bool Down(int key) => (GetKeyState(key) & 0x8000) != 0;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var key = (int)info.VkCode;
            var alt = Down(VK_MENU);
            var ctrl = Down(VK_CONTROL);
            var shift = Down(VK_SHIFT);

            // Acceso técnico permitido.
            if (key == VK_F12 && ctrl && shift && !alt) return CallNextHookEx(_hook, nCode, wParam, lParam);

            var block = key is VK_LWIN or VK_RWIN
                        || (key == VK_TAB && alt)
                        || (key == VK_ESCAPE && (alt || ctrl))
                        || (key == VK_F4 && alt);

            if (block) return (IntPtr)1;
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}
