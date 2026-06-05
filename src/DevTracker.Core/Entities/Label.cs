using DevTracker.Core.Entities.Base;

namespace DevTracker.Core.Entities;

/// <summary>Label de classificação reutilizável entre work items.</summary>
public class Label : BaseEntity
{
    public string Name  { get; set; } = string.Empty;
    /// <summary>Cor em hex (#RRGGBB) para representação visual na UI.</summary>
    public string Color { get; set; } = "#6366F1";

    // Navegação
    public ICollection<WorkItemLabel> WorkItems { get; set; } = [];
}

/// <summary>
/// Tabela de junção entre WorkItem e Label.
/// Chave composta (WorkItemId, LabelId) configurada via EF Fluent API.
/// </summary>
public class WorkItemLabel
{
    public Guid WorkItemId { get; set; }
    public Guid LabelId    { get; set; }

    // Navegação
    public WorkItem WorkItem { get; set; } = null!;
    public Label    Label    { get; set; } = null!;
}
