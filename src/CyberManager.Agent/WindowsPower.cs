using System.Runtime.InteropServices;

namespace CyberManager.Agent;

public class AgentOptions
{
    public string ServerUrl { get; set; } = "http://127.0.0.1:5080";
    public int StationId { get; set; }
    public string Token { get; set; } = string.Empty;
    public int HeartbeatSeconds { get; set; } = 10;
    public bool IsConfigured => StationId > 0 && !string.IsNullOrWhiteSpace(Token);
}

/// <summary>Apagado, reinicio y cierre de sesión con los privilegios del servicio.</summary>
public static class WindowsPower
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TokPriv1Luid
    {
        public int Count;
        public long Luid;
        public int Attr;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? host, string name, ref long pluid);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    private const int TOKEN_QUERY = 0x00000008;
    private const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
    private const int SE_PRIVILEGE_ENABLED = 0x00000002;
    private const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

    private const uint EWX_LOGOFF = 0x00000000;
    private const uint EWX_SHUTDOWN = 0x00000001;
    private const uint EWX_REBOOT = 0x00000002;
    private const uint EWX_FORCE = 0x00000004;
    private const uint EWX_POWEROFF = 0x00000008;

    private static bool EnablePrivilege()
    {
        var htok = IntPtr.Zero;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok))
            return false;

        var tp = new TokPriv1Luid { Count = 1, Attr = SE_PRIVILEGE_ENABLED };
        if (!LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, ref tp.Luid)) return false;
        return AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
    }

    public static bool Shutdown(bool restart) => Execute(restart ? EWX_REBOOT : EWX_SHUTDOWN, restart);
    public static bool Logoff() => Execute(EWX_LOGOFF, false);

    private static bool Execute(uint flag, bool restart)
    {
        EnablePrivilege();
        var ok = ExitWindowsEx(flag | EWX_FORCE, restart ? 0u : 0u);
        if (!ok) ok = ExitWindowsEx(flag | EWX_FORCE | EWX_POWEROFF, 0);
        return ok;
    }
}
