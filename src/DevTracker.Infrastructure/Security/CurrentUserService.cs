using DevTracker.Application.Abstractions.Security;
using DevTracker.Core.Enums;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Acesso ao utilizador autenticado atual, derivado do ISessionService. Singleton.
/// Todos os serviços de aplicação usam esta interface para obter o contexto do utilizador.
/// </summary>
public sealed class CurrentUserService(ISessionService session) : ICurrentUserService
{
    public Guid?     UserId          => session.Current?.UserId;
    public string?   Username        => session.Current?.Username;
    public UserRole? Role            => session.Current?.Role;
    public bool      IsAuthenticated => session.IsAuthenticated;
}
