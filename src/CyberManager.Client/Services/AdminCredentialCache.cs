using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;

namespace CyberManager.Client.Services;

/// <summary>
/// Credencial de administrador guardada en este equipo para poder desbloquear la
/// estación cuando el servidor no responde. La contraseña no se guarda: se guarda
/// un hash PBKDF2-SHA256 con sal aleatoria, y todo el archivo se cifra con DPAPI
/// (ámbito del usuario de Windows de la estación).
///
/// Solo se usa cuando la red falla; una contraseña rechazada por el servidor
/// (401) nunca cae en la caché local. Hay un límite de intentos para que nadie
/// pueda probar contraseñas a ciegas.
/// </summary>
public static class AdminCredentialCache
{
    private const int Iterations = 150_000;
    private const int HashBytes = 32;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(5);
    private static readonly object Gate = new();

    public enum Result
    {
        /// <summary>Usuario y contraseña correctos.</summary>
        Ok,
        /// <summary>No coinciden con la credencial guardada.</summary>
        Invalid,
        /// <summary>Demasiados intentos fallidos; hay que esperar.</summary>
        LockedOut,
        /// <summary>Este equipo no tiene credencial guardada (nunca hubo login con red).</summary>
        NotAvailable
    }

    private sealed class Entry
    {
        public string Username { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public int Iterations { get; set; } = AdminCredentialCache.Iterations;
        public int FailedAttempts { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
    }

    private static string FilePath => Path.Combine(AppPaths.DataDir("Client"), "admin-offline.dat");

    /// <summary>Indica si el equipo puede desbloquearse sin conexión.</summary>
    public static bool IsAvailable => Load() is not null;

    /// <summary>
    /// Guarda la credencial tras un inicio de sesión correcto contra el servidor.
    /// Si la contraseña no cambió, no se reescribe el archivo.
    /// </summary>
    public static void Seed(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return;

        lock (Gate)
        {
            try
            {
                var current = Load();
                if (current is not null &&
                    string.Equals(current.Username, username, StringComparison.OrdinalIgnoreCase) &&
                    Matches(current, password))
                    return;

                var salt = RandomNumberGenerator.GetBytes(16);
                var entry = new Entry
                {
                    Username = username.Trim(),
                    Salt = Convert.ToBase64String(salt),
                    Hash = Convert.ToBase64String(Derive(password, salt, Iterations)),
                    Iterations = Iterations
                };

                Save(entry);
            }
            catch (Exception ex)
            {
                ClientLog.Error("No se pudo guardar la credencial de administrador local", ex);
            }
        }
    }

    /// <summary>Comprueba la contraseña contra la credencial guardada en el equipo.</summary>
    public static Result Verify(string username, string password)
    {
        lock (Gate)
        {
            var entry = Load();
            if (entry is null) return Result.NotAvailable;

            if (entry.LockedUntilUtc is DateTime until)
            {
                if (until > DateTime.UtcNow) return Result.LockedOut;

                // La espera terminó: se limpia para que el próximo fallo cuente
                // desde cero en lugar de responder «bloqueado» para siempre.
                entry.LockedUntilUtc = null;
                entry.FailedAttempts = 0;
                try { Save(entry); } catch { /* solo se pierde el contador */ }
            }

            var ok = string.Equals(entry.Username, username.Trim(), StringComparison.OrdinalIgnoreCase) &&
                     Matches(entry, password);

            if (ok)
            {
                if (entry.FailedAttempts != 0 || entry.LockedUntilUtc is not null)
                {
                    entry.FailedAttempts = 0;
                    entry.LockedUntilUtc = null;
                    try { Save(entry); } catch { /* solo se pierde el contador */ }
                }

                return Result.Ok;
            }

            entry.FailedAttempts++;
            if (entry.FailedAttempts >= MaxAttempts)
            {
                entry.FailedAttempts = 0;
                entry.LockedUntilUtc = DateTime.UtcNow.Add(Lockout);
            }

            try { Save(entry); } catch { /* solo se pierde el contador */ }
            return entry.LockedUntilUtc is not null ? Result.LockedOut : Result.Invalid;
        }
    }

    /// <summary>Minutos que faltan para poder reintentar tras demasiados fallos.</summary>
    public static int LockoutMinutesLeft()
    {
        var entry = Load();
        if (entry?.LockedUntilUtc is not DateTime until) return 0;
        var left = until - DateTime.UtcNow;
        return left <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(left.TotalMinutes);
    }

    private static bool Matches(Entry entry, string password)
    {
        try
        {
            var salt = Convert.FromBase64String(entry.Salt);
            var expected = Convert.FromBase64String(entry.Hash);
            var actual = Derive(password, salt, entry.Iterations);
            return expected.Length == actual.Length &&
                   CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt,
            Math.Max(10_000, iterations), HashAlgorithmName.SHA256, HashBytes);

    private static Entry? Load()
    {
        try
        {
            var file = FilePath;
            if (!File.Exists(file)) return null;

            var clear = TokenProtector.Unprotect(File.ReadAllText(file));
            if (string.IsNullOrWhiteSpace(clear)) return null;

            var entry = JsonSerializer.Deserialize<Entry>(clear);
            return string.IsNullOrWhiteSpace(entry?.Username) ||
                   string.IsNullOrWhiteSpace(entry.Salt) ||
                   string.IsNullOrWhiteSpace(entry.Hash)
                ? null
                : entry;
        }
        catch
        {
            return null;
        }
    }

    private static void Save(Entry entry)
    {
        var json = JsonSerializer.Serialize(entry);
        var protectedValue = TokenProtector.Protect(json);
        if (protectedValue is null) return;

        var file = FilePath;
        var temp = file + ".tmp";
        File.WriteAllText(temp, protectedValue);
        File.Move(temp, file, overwrite: true);
    }
}
