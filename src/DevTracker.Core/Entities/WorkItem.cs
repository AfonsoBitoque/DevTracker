using DevTracker.Core.Entities.Base;
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

/// <summary>
/// Tarefa/issue de um projeto. Number e sequencial por projeto (gerado no servico, nao pelo ORM).
/// Soft delete: IsDeleted=true + DeletedAt.
/// </summary>
public class WorkItem : BaseEntity
{
    public Guid             ProjectId        { get; set; }
    /// <summary>Numero sequencial unico por projeto. Gerado como MAX(Number)+1 em WorkItemService.</summary>
    public int              Number           { get; set; }
    public string           Title            { get; set; } = string.Empty;
    public string?          Description      { get; set; }
    public WorkItemType          Type             { get; set; } = WorkItemType.Task;
    public WorkItemStatus        Status           { get; set; } = WorkItemStatus.Backlog;
    public WorkItemPriority      Priority         { get; set; } = WorkItemPriority.Medium;
    public WorkItemDifficulty    Difficulty       { get; set; } = WorkItemDifficulty.Medium;
    public WorkItemEstimatedTime EstimatedTime    { get; set; } = WorkItemEstimatedTime.OneDay;
    public Guid                  CreatedByUserId  { get; set; }
    public Guid?            AssignedToUserId { get; set; }
    public DateTime?        DueDate          { get; set; }
    public bool             IsDeleted        { get; set; } = false;
    public DateTime?        DeletedAt        { get; set; }
    public string?          AiPromptUsed     { get; set; }
    public string?          MomentumNote     { get; set; }
    public int              EstimatedPrompts { get; set; }
    public int              ActualPrompts    { get; set; }

    // Navegacao
    public Project           Project        { get; set; } = null!;
    public User              CreatedByUser  { get; set; } = null!;
    public User?             AssignedToUser { get; set; }
    public ICollection<Comment>      Comments { get; set; } = [];
    public ICollection<WorkItemLabel> Labels  { get; set; } = [];
}
