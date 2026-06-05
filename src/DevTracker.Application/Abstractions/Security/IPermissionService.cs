namespace DevTracker.Application.Abstractions.Security;

/// <summary>
/// Serviço central de autorização. Toda a verificação de permissões passa por aqui.
/// Nunca usar role comparisons diretas no código — delegar sempre a este serviço.
/// </summary>
public interface IPermissionService
{
    /// <summary>Verifica se o utilizador pode executar a operação. Para uso em fluxos normais e UI adaptativa.</summary>
    Task<bool> CanPerformAsync(Guid userId, string permission, CancellationToken ct = default);

    /// <summary>
    /// Garante que o utilizador tem permissão. Lança <see cref="Common.AuthorizationException"/> se não tiver.
    /// Usar nos serviços de aplicação antes de qualquer lógica de negócio.
    /// </summary>
    Task EnsureCanPerformAsync(Guid userId, string permission, CancellationToken ct = default);
}
