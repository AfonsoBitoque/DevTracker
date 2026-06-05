using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.Security;

/// <summary>Resultados de operações de autenticação com informação de sessão.</summary>
public sealed record AuthResult(bool Succeeded, string? Error = null, SessionInfo? Session = null)
{
    public static AuthResult Success(SessionInfo session) => new(true, null, session);
    public static AuthResult Fail(string error)           => new(false, error);
}

/// <summary>Snapshot público da sessão ativa (exposto à UI e serviços).</summary>
public sealed record SessionInfo(
    Guid   UserId,
    string Username,
    string Role,
    bool   IsLocked);

/// <summary>
/// Gestão do ciclo de vida de autenticação local.
/// SetupFirstOwnerAsync só funciona uma vez (quando não existem utilizadores).
/// </summary>
public interface IAuthService
{
    /// <summary>Cria o primeiro utilizador Owner. Falha se já existirem utilizadores.</summary>
    Task<AuthResult> SetupFirstOwnerAsync(string username, string password, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default);
    Task<Result>     LogoutAsync(CancellationToken ct = default);
    Task<Result>     ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);
    Task<bool>       AnyUsersExistAsync(CancellationToken ct = default);
}
