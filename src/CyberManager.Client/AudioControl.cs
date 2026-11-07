using System.Runtime.InteropServices;

namespace CyberManager.Client;

/// <summary>
/// Volumen maestro de Windows mediante Core Audio. No requiere paquetes externos
/// y funciona en Windows 10/11 x86 y x64.
/// </summary>
public static class AudioControl
{
    private enum EDataFlow
    {
        Render = 0
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        [PreserveSig] int GetDevice(string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out int count);
        [PreserveSig] int SetMasterVolumeLevel(float level, Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private const int ClsCtxAll = 23;

    private static IAudioEndpointVolume? Endpoint()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0)
                return null;

            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var instance) != 0)
                return null;

            return instance as IAudioEndpointVolume;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Ajusta el volumen maestro. <paramref name="percent"/> se limita a 0-100.</summary>
    public static bool SetVolume(int percent)
    {
        var endpoint = Endpoint();
        if (endpoint is null) return false;

        var level = Math.Clamp(percent, 0, 100) / 100f;
        return endpoint.SetMasterVolumeLevelScalar(level, Guid.Empty) == 0;
    }

    public static bool SetMute(bool mute)
    {
        var endpoint = Endpoint();
        return endpoint?.SetMute(mute, Guid.Empty) == 0;
    }
}
