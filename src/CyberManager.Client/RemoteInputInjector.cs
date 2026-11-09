using System.Runtime.InteropServices;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

/// <summary>
/// Inyecta eventos de mouse y teclado enviados desde la consola de administración.
/// Se ejecuta en la sesión interactiva del cliente, que es la única que puede
/// controlar el escritorio del usuario.
/// </summary>
public static class RemoteInputInjector
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    // La unión debe quedar en el desplazamiento natural (8 bytes en x64 y 4 en x86).
    // Usar LayoutKind.Sequential evita un desplazamiento fijo que solo funcionaba en x86.
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void Handle(RemoteInputEvent input)
    {
        switch (input.Kind)
        {
            case "move":
                Move(input.X, input.Y);
                break;
            case "button":
                Button(input.Button, input.Down);
                break;
            case "wheel":
                Wheel(input.Wheel);
                break;
            case "key":
                Key(input.KeyCode, input.Down);
                break;
        }
    }

    private static void Move(double x, double y)
    {
        var nx = (int)Math.Round(Math.Clamp(x, 0, 1) * 65535);
        var ny = (int)Math.Round(Math.Clamp(y, 0, 1) * 65535);
        Send(new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = nx,
                    dy = ny,
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
                }
            }
        });
    }

    private static void Button(int button, bool down)
    {
        var flag = button switch
        {
            1 => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            2 => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP
        };
        Send(new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flag } }
        });
    }

    private static void Wheel(int delta)
    {
        Send(new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT { mouseData = (uint)delta, dwFlags = MOUSEEVENTF_WHEEL }
            }
        });
    }

    private static void Key(int keyCode, bool down)
    {
        if (keyCode <= 0) return;
        Send(new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)keyCode,
                    dwFlags = down ? 0u : KEYEVENTF_KEYUP
                }
            }
        });
    }

    private static void Send(INPUT input)
    {
        var buffer = new[] { input };
        SendInput(1, buffer, Marshal.SizeOf<INPUT>());
    }
}
