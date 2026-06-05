using DevTracker.Application.Abstractions.Security;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Gestão da sessão ativa em memória. Singleton.
/// Timeout de inatividade: 15 min. Expiração dura: 8h.
/// A sessão nunca é persistida em disco.
/// </summary>
public sealed class InMemorySessionService : ISessionService
{
    private static readonly TimeSpan IdleTimeout   = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxDuration   = TimeSpan.FromHours(8);

    private UserSession? _session;
    private bool         _isLocked;

    public UserSession? Current         => _session;
    public bool         IsAuthenticated => _session is not null && !_isLocked && !IsExpired();
    public bool         IsLocked        => _isLocked;

    public void Start(UserSession session)
    {
        _session  = session;
        _isLocked = false;
    }

    public void End()
    {
        _session  = null;
        _isLocked = false;
    }

    /// <summary>Atualiza a última atividade. Deve ser chamado a cada interação do utilizador.</summary>
    public void Touch()
    {
        if (_session is null) return;
        _session = _session with { LastActivityUtc = DateTime.UtcNow };
    }

    public void Lock() => _isLocked = true;

    public bool IsExpired()
    {
        if (_session is null) return true;
        var now = DateTime.UtcNow;
        return now > _session.ExpiresAtUtc
            || now - _session.LastActivityUtc > IdleTimeout;
    }
}
