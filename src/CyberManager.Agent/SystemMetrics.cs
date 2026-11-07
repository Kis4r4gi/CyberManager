using System.Runtime.InteropServices;

namespace CyberManager.Agent;

/// <summary>Lecturas nativas de usuario activo, CPU y memoria sin dependencias externas.</summary>
public sealed class SystemMetrics
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
        public ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr server, uint sessionId, int infoClass,
        out IntPtr buffer, out uint bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    private const int WTSUserName = 5;
    private const int WTSDomainName = 7;

    private ulong _lastIdle, _lastKernel, _lastUser;
    private double _lastCpu;

    public double CpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;

        var idleNow = idle.ToUInt64();
        var kernelNow = kernel.ToUInt64();
        var userNow = user.ToUInt64();

        if (_lastKernel == 0 && _lastUser == 0)
        {
            _lastIdle = idleNow;
            _lastKernel = kernelNow;
            _lastUser = userNow;
            return 0;
        }

        var idleDelta = idleNow - _lastIdle;
        var totalDelta = (kernelNow - _lastKernel) + (userNow - _lastUser);
        _lastIdle = idleNow;
        _lastKernel = kernelNow;
        _lastUser = userNow;

        if (totalDelta == 0) return _lastCpu;
        _lastCpu = Math.Round(100.0 * (totalDelta - idleDelta) / totalDelta, 1);
        return Math.Clamp(_lastCpu, 0, 100);
    }

    public double RamPercent()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref mem) || mem.ullTotalPhys == 0) return 0;
        return Math.Round(100.0 * (mem.ullTotalPhys - mem.ullAvailPhys) / mem.ullTotalPhys, 1);
    }

    public string CurrentUser()
    {
        try
        {
            var sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == 0xFFFFFFFF) return string.Empty;

            var user = Query(sessionId, WTSUserName);
            var domain = Query(sessionId, WTSDomainName);
            if (string.IsNullOrWhiteSpace(user)) return string.Empty;
            return string.IsNullOrWhiteSpace(domain) ? user : $"{domain}\\{user}";
        }
        catch
        {
            return Environment.UserName;
        }
    }

    private static string Query(uint sessionId, int infoClass)
    {
        if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, infoClass, out var buffer, out _)) return string.Empty;
        try { return Marshal.PtrToStringUni(buffer) ?? string.Empty; }
        finally { WTSFreeMemory(buffer); }
    }
}
