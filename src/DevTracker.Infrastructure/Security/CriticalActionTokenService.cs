using System.Collections.Concurrent;
using DevTracker.Application.Abstractions.Security;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Tokens de ação crítica single-use, 30s de validade, 100% em memória. Singleton.
/// Fluxo: UI faz re-auth → CreateToken → passa Guid ao serviço → ConsumeToken.
/// </summary>
public sealed class CriticalActionTokenService : ICriticalActionTokenService
{
    private static readonly TimeSpan TokenValidity = TimeSpan.FromSeconds(30);

    private record TokenEntry(Guid UserId, string Permission, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<Guid, TokenEntry> _tokens = new();

    public Guid CreateToken(Guid userId, string permission)
    {
        // Clear tokens expirados antes de criar um novo
        PurgeExpired();

        var token = Guid.NewGuid();
        _tokens[token] = new TokenEntry(userId, permission, DateTime.UtcNow.Add(TokenValidity));
        return token;
    }

    public bool ConsumeToken(Guid token, Guid userId, string permission)
    {
        if (!_tokens.TryRemove(token, out var entry))
            return false;

        return entry.UserId     == userId
            && entry.Permission == permission
            && DateTime.UtcNow  <= entry.ExpiresAt;
    }

    private void PurgeExpired()
    {
        var now     = DateTime.UtcNow;
        var expired = _tokens.Where(kv => kv.Value.ExpiresAt < now).Select(kv => kv.Key).ToList();
        foreach (var key in expired)
            _tokens.TryRemove(key, out _);
    }
}
