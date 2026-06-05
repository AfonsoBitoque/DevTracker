using DevTracker.Core.Enums;

namespace DevTracker.Application.Abstractions.Security;

/// <summary>
/// Acesso ao utilizador autenticado atual. Singleton derivado da ISessionService.
/// Usar nos serviços para obter o contexto do utilizador sem depender diretamente da sessão.
/// </summary>
public interface ICurrentUserService
{
    Guid?    UserId          { get; }
    string?  Username        { get; }
    UserRole? Role           { get; }
    bool     IsAuthenticated { get; }
}
