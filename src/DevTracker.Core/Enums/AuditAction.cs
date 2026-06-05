namespace DevTracker.Core.Enums;

/// <summary>
/// Categorias de ações auditáveis. Intervalos por domínio:
/// 100–199 = Auth, 200–299 = Project, 300–399 = Repository, 400–499 = WorkItem, 500–599 = User.
/// AuditEntry é imutável — só INSERT, nunca UPDATE/DELETE.
/// </summary>
public enum AuditAction
{
    // Auth
    Login           = 100,
    Logout          = 101,
    PasswordChanged = 102,

    // Project
    ProjectCreated      = 200,
    ProjectUpdated      = 201,
    ProjectStateChanged = 202,
    ProjectDeleted      = 203,

    // Repository
    RepositoryCreated = 300,
    RepositoryRenamed = 301,
    RepositoryDeleted = 302,

    // WorkItem
    WorkItemCreated       = 400,
    WorkItemUpdated       = 401,
    WorkItemStatusChanged = 402,
    WorkItemDeleted       = 403,

    // User
    UserCreated    = 500,
    UserUpdated    = 501,
    UserDeactivated = 502
}
