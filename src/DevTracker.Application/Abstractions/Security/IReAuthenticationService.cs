using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.Security;

/// <summary>
/// Re-autenticação para operações destrutivas. Implementada pela mesma classe AuthService.
/// Fluxo: UI recolhe password → ConfirmPasswordAsync → se sucesso, executar operação.
/// Para operações que passam o token ao serviço, usar ICriticalActionTokenService.
/// </summary>
public interface IReAuthenticationService
{
    Task<Result> ConfirmPasswordAsync(Guid userId, string password, CancellationToken ct = default);
}
