using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Common;
using DevTracker.Application.Security;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Implementação da autorização baseada em matriz estática Role → permissões.
/// Faz 1 query à BD por verificação para obter a role do utilizador.
/// Para UI adaptativa, usar IPermissionSnapshotService (cache por instância).
/// </summary>
public sealed class PermissionService(AppDbContext db) : IPermissionService
{
    /// <summary>Matriz imutável de Role → conjunto de permissões permitidas.</summary>
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Matrix = BuildMatrix();

    public async Task<bool> CanPerformAsync(Guid userId, string permission, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);

        return user is not null
            && Matrix.TryGetValue(user.Role, out var perms)
            && perms.Contains(permission);
    }

    public async Task EnsureCanPerformAsync(Guid userId, string permission, CancellationToken ct = default)
    {
        if (!await CanPerformAsync(userId, permission, ct))
            throw new AuthorizationException($"O utilizador não tem permissão '{permission}'.");
    }

    /// <summary>Expõe a matriz para uso no PermissionSnapshotService sem duplicação.</summary>
    internal static IReadOnlyDictionary<UserRole, HashSet<string>> GetMatrix() => Matrix;

    private static Dictionary<UserRole, HashSet<string>> BuildMatrix() => new()
    {
        [UserRole.Owner] = [
            Permissions.ProjectCreate, Permissions.ProjectRead, Permissions.ProjectUpdate,
            Permissions.ProjectRename, Permissions.ProjectChangeState, Permissions.ProjectDelete, Permissions.ProjectArchive,
            Permissions.RepositoryCreate, Permissions.RepositoryRead, Permissions.RepositoryRename,
            Permissions.RepositoryDelete, Permissions.RepositoryAttach,
            Permissions.WorkItemCreate, Permissions.WorkItemRead, Permissions.WorkItemUpdate,
            Permissions.WorkItemDelete, Permissions.WorkItemAssign, Permissions.WorkItemChangeStatus, Permissions.WorkItemComment,
            Permissions.UserRead, Permissions.UserCreate, Permissions.UserUpdate, Permissions.UserDelete, Permissions.UserChangeRole,
            Permissions.SecurityChangePassword, Permissions.SecurityReAuthenticate,
            Permissions.AuditRead, Permissions.SettingsRead, Permissions.SettingsUpdate
        ],
        [UserRole.Admin] = [
            Permissions.ProjectCreate, Permissions.ProjectRead, Permissions.ProjectUpdate,
            Permissions.ProjectRename, Permissions.ProjectChangeState, Permissions.ProjectArchive,
            Permissions.RepositoryCreate, Permissions.RepositoryRead, Permissions.RepositoryRename,
            Permissions.RepositoryDelete, Permissions.RepositoryAttach,
            Permissions.WorkItemCreate, Permissions.WorkItemRead, Permissions.WorkItemUpdate,
            Permissions.WorkItemDelete, Permissions.WorkItemAssign, Permissions.WorkItemChangeStatus, Permissions.WorkItemComment,
            Permissions.UserRead, Permissions.SecurityChangePassword, Permissions.SecurityReAuthenticate,
            Permissions.AuditRead, Permissions.SettingsRead, Permissions.SettingsUpdate
        ],
        [UserRole.Maintainer] = [
            Permissions.ProjectRead, Permissions.ProjectUpdate, Permissions.ProjectChangeState,
            Permissions.RepositoryCreate, Permissions.RepositoryRead, Permissions.RepositoryRename, Permissions.RepositoryAttach,
            Permissions.WorkItemCreate, Permissions.WorkItemRead, Permissions.WorkItemUpdate,
            Permissions.WorkItemAssign, Permissions.WorkItemChangeStatus, Permissions.WorkItemComment,
            Permissions.UserRead, // Leitura limitada: lookup de username/role para atribuição
            Permissions.SecurityChangePassword, Permissions.SecurityReAuthenticate,
            Permissions.SettingsRead
        ],
        [UserRole.Reader] = [
            Permissions.ProjectRead, Permissions.RepositoryRead,
            Permissions.WorkItemRead, Permissions.WorkItemComment,
            Permissions.SecurityChangePassword, Permissions.SecurityReAuthenticate,
            Permissions.SettingsRead
        ]
    };
}
