using DevTracker.Application.Abstractions.Security;
using DevTracker.Core.Enums;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Cache de permissões por instância (Scoped). Evita N queries à BD por ecrã nos ViewModels.
/// Usar no OnActivatedAsync dos ViewModels para carregar todas as permissões de uma vez.
/// NÃO substitui IPermissionService nos serviços — é apenas para adaptar a UI.
/// </summary>
public sealed class PermissionSnapshotService(ICurrentUserService currentUser) : IPermissionSnapshotService
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Matrix = PermissionService.GetMatrix();

    private HashSet<string>? _cached;
    private Guid?            _cachedForUserId;

    public Task<HashSet<string>> GetCurrentUserPermissionsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;

        // Retornar cache se o utilizador não mudou
        if (_cached is not null && _cachedForUserId == userId)
            return Task.FromResult(_cached);

        if (userId is null || currentUser.Role is null)
        {
            _cached          = [];
            _cachedForUserId = null;
            return Task.FromResult(_cached);
        }

        _cached = Matrix.TryGetValue(currentUser.Role.Value, out var perms)
            ? new HashSet<string>(perms)
            : [];

        _cachedForUserId = userId;
        return Task.FromResult(_cached);
    }

    public void InvalidateCache()
    {
        _cached          = null;
        _cachedForUserId = null;
    }
}
