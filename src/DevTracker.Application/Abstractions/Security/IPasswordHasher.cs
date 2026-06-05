namespace DevTracker.Application.Abstractions.Security;

/// <summary>Resultado do hashing: hash em Base64 e salt aleatório em Base64.</summary>
public sealed record PasswordHashResult(string Hash, string Salt);

/// <summary>
/// Hashing de passwords com Argon2id. Implementação em Infrastructure.
/// Configuração: TimeCost=4, MemoryCost=65536, Lanes=4, HashLength=32.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Gera um novo hash com salt aleatório de 32 bytes.</summary>
    PasswordHashResult Hash(string password);

    /// <summary>Verifica se a password corresponde ao hash+salt armazenados.</summary>
    bool Verify(string password, string hash, string salt);
}
