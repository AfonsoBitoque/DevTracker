using DevTracker.Application.Abstractions.Security;
using DevTracker.Core.Enums;

namespace DevTracker.Application.Abstractions.Security;

/// <summary>Sessão completa gerida em memória. Nunca persistida em disco.</summary>
public sealed record UserSession(
    Guid     Token,
    Guid     UserId,
    string   Username,
    UserRole Role,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime LastActivityUtc);

/// <summary>
/// Gere a sessão ativa em memória. Singleton — estado global da aplicação.
/// Timeout de inatividade: 15 min. Expiração dura: 8h.
/// </summary>
public interface ISessionService
{
    UserSession? Current         { get; }
    bool         IsAuthenticated { get; }
    bool         IsLocked        { get; }

    void Start(UserSession session);
    void End();
    /// <summary>Atualiza LastActivityUtc. Chamar em qualquer interação do utilizador.</summary>
    void Touch();
    void Lock();
    bool IsExpired();
}
