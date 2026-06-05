namespace DevTracker.Application.Abstractions.Security;

/// <summary>
/// Tokens de ação crítica: single-use, 30s de validade, 100% em memória.
/// Fluxo: UI faz re-auth → CreateToken → passa o Guid ao serviço → serviço chama ConsumeToken.
/// Evita passar passwords em plaintext através de chamadas de serviço.
/// </summary>
public interface ICriticalActionTokenService
{
    /// <summary>Cria um token para o utilizador e permissão indicados. Válido por 30 segundos.</summary>
    Guid CreateToken(Guid userId, string permission);

    /// <summary>
    /// Consome e invalida o token. Retorna false se expirado, já usado, ou não pertencer ao utilizador/permissão.
    /// </summary>
    bool ConsumeToken(Guid token, Guid userId, string permission);
}
