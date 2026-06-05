namespace DevTracker.Application.Security;

/// <summary>
/// Constantes de operações protegidas. Usar sempre estas constantes — nunca strings literais.
/// A matriz de mapeamento Role → permissões vive em PermissionService (Infrastructure).
/// </summary>
public static class Permissions
{
    // Projects
    public const string ProjectCreate      = "project.create";
    public const string ProjectRead        = "project.read";
    public const string ProjectUpdate      = "project.update";
    public const string ProjectRename      = "project.rename";
    public const string ProjectChangeState = "project.change-state";
    public const string ProjectDelete      = "project.delete";
    public const string ProjectArchive     = "project.archive";

    // Repositórios
    public const string RepositoryCreate = "repository.create";
    public const string RepositoryRead   = "repository.read";
    public const string RepositoryRename = "repository.rename";
    public const string RepositoryDelete = "repository.delete";
    public const string RepositoryAttach = "repository.attach";

    // Work Items
    public const string WorkItemCreate       = "workitem.create";
    public const string WorkItemRead         = "workitem.read";
    public const string WorkItemUpdate       = "workitem.update";
    public const string WorkItemDelete       = "workitem.delete";
    public const string WorkItemAssign       = "workitem.assign";
    public const string WorkItemChangeStatus = "workitem.change-status";
    public const string WorkItemComment      = "workitem.comment";

    // Usernamees & Security
    public const string UserRead             = "user.read";
    public const string UserCreate           = "user.create";
    public const string UserUpdate           = "user.update";
    public const string UserDelete           = "user.delete";
    public const string UserChangeRole       = "user.change-role";
    public const string SecurityChangePassword  = "security.change-password";
    public const string SecurityReAuthenticate  = "security.re-authenticate";

    // Audit & Settings
    public const string AuditRead      = "audit.read";
    public const string SettingsRead   = "settings.read";
    public const string SettingsUpdate = "settings.update";
}
