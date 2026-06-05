using DevTracker.Core.Entities.Base;

namespace DevTracker.Core.Entities;

/// <summary>
/// Comentário num work item. Soft delete: IsDeleted=true.
/// EditedAt fica preenchido quando o conteúdo é alterado após criação.
/// </summary>
public class Comment : BaseEntity
{
    public Guid      WorkItemId  { get; set; }
    public Guid      AuthorUserId { get; set; }
    public string    Body        { get; set; } = string.Empty;
    public DateTime? EditedAt    { get; set; }
    public bool      IsDeleted   { get; set; } = false;
    public DateTime? DeletedAt   { get; set; }

    // Navegação
    public WorkItem WorkItem     { get; set; } = null!;
    public User     AuthorUser   { get; set; } = null!;
}
