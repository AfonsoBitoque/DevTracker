using DevTracker.Core.Entities.Base;
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

/// <summary>
/// Utilizador local do sistema. Autenticação via Argon2id (PasswordHash + Salt).
/// A sessão é gerida em memória por ISessionService — nunca persistida.
/// </summary>
public class User : BaseEntity
{
    public string   Username     { get; set; } = string.Empty;
    public string   PasswordHash { get; set; } = string.Empty;
    public string   Salt         { get; set; } = string.Empty;
    public UserRole Role         { get; set; } = UserRole.Reader;
    public bool     IsActive     { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // Navegação — WorkItems criados e atribuídos a este utilizador
    public ICollection<WorkItem> CreatedWorkItems  { get; set; } = [];
    public ICollection<WorkItem> AssignedWorkItems { get; set; } = [];
    public ICollection<Comment>  Comments          { get; set; } = [];
}
