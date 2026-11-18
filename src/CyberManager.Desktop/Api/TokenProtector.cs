using System.Runtime.InteropServices;
using System.Text;

namespace CyberManager.Desktop.Api;

/// <summary>
/// Cifra el token de la estación con DPAPI (ámbito del usuario actual) mediante
/// crypt32.dll, sin agregar dependencias. Si el sistema no puede cifrar, el
/// llamador conserva el token en el formato anterior para no perder el acceso.
/// </summary>
public static class TokenProtector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    /// <summary>Cifra un texto y devuelve Base64, o null si DPAPI no está disponible.</summary>
    public static string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        var bytes = Encoding.UTF8.GetBytes(value);
        var input = new DataBlob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            if (!CryptProtectData(ref input, "CyberManager", IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, 0, out var output))
                return null;

            try
            {
                var protectedBytes = new byte[output.Size];
                Marshal.Copy(output.Data, protectedBytes, 0, output.Size);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                LocalFree(output.Data);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
        }
    }

    /// <summary>Descifra un texto protegido con <see cref="Protect"/>, o null si no se puede.</summary>
    public static string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return null;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(protectedValue); }
        catch { return null; }

        var input = new DataBlob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, 0, out var output))
                return null;

            try
            {
                var clear = new byte[output.Size];
                Marshal.Copy(output.Data, clear, 0, output.Size);
                return Encoding.UTF8.GetString(clear);
            }
            finally
            {
                LocalFree(output.Data);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
        }
    }
}
