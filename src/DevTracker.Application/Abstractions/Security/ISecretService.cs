using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.Security;

/// <summary>
/// Serviço para armazenamento seguro de segredos em disco.
/// Usa encriptação via Microsoft.AspNetCore.DataProtection.
/// </summary>
public interface ISecretService
{
    /// <summary>Guarda um segredo encriptado com a chave especificada.</summary>
    Task<Result> SaveSecretAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Recupera e desencripta um segredo pela chave. Retorna null se não existir.</summary>
    Task<Result<string?>> GetSecretAsync(string key, CancellationToken ct = default);
}
