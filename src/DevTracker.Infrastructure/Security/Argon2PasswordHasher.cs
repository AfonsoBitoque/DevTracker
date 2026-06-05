using DevTracker.Application.Abstractions.Security;
using Isopoh.Cryptography.Argon2;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Hashing de passwords com Argon2id.
/// Parâmetros: TimeCost=4, MemoryCost=65536 KB, Lanes=4, HashLength=32 bytes.
/// Salt é gerado aleatoriamente (32 bytes) por cada chamada a Hash().
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltLength   = 32;
    private const int HashLength   = 32;
    private const int TimeCost     = 4;
    private const int MemoryCost   = 65536;
    private const int Parallelism  = 4;

    public PasswordHashResult Hash(string password)
    {
        var salt = new byte[SaltLength];
        System.Security.Cryptography.RandomNumberGenerator.Fill(salt);

        var config = new Argon2Config
        {
            Type        = Argon2Type.HybridAddressing, // Argon2id
            Version     = Argon2Version.Nineteen,
            Password    = System.Text.Encoding.UTF8.GetBytes(password),
            Salt        = salt,
            TimeCost    = TimeCost,
            MemoryCost  = MemoryCost,
            Lanes       = Parallelism,
            Threads     = Parallelism,
            HashLength  = HashLength
        };

        using var hasher = new Argon2(config);
        using var hash   = hasher.Hash();

        return new PasswordHashResult(
            Hash: Convert.ToBase64String(hash.Buffer),
            Salt: Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(salt);

            var config = new Argon2Config
            {
                Type        = Argon2Type.HybridAddressing,
                Version     = Argon2Version.Nineteen,
                Password    = System.Text.Encoding.UTF8.GetBytes(password),
                Salt        = saltBytes,
                TimeCost    = TimeCost,
                MemoryCost  = MemoryCost,
                Lanes       = Parallelism,
                Threads     = Parallelism,
                HashLength  = HashLength
            };

            using var hasher   = new Argon2(config);
            using var computed = hasher.Hash();

            var computedBase64 = Convert.ToBase64String(computed.Buffer);

            // Comparação constant-time para evitar timing attacks
            return CryptographicEquals(computedBase64, hash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Comparação de strings em tempo constante para prevenir timing attacks.</summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
