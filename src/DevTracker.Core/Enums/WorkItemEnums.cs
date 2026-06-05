namespace DevTracker.Core.Enums;

/// <summary>Tipo de work item, determina a categoria visual e semântica.</summary>
public enum WorkItemType
{
    Task          = 0,
    Feature       = 1,
    Bug           = 2,
    Improvement   = 3,
    Documentation = 4
}

/// <summary>
/// Estado de progressão de um work item no fluxo de trabalho.
/// Transições são livres (sem máquina de estados rígida neste nível).
/// </summary>
public enum WorkItemStatus
{
    Backlog   = 0,
    Todo      = 1,
    InProgress = 2,
    InReview  = 3,
    Done      = 4,
    Cancelled = 5
}

/// <summary>Prioridade de um work item. Influencia ordenação e visualização no Kanban.</summary>
public enum WorkItemPriority
{
    Low      = 0,
    Medium   = 1,
    High     = 2,
    Critical = 3
}

/// <summary>Dificuldade de um work item (estimativa de complexidade).</summary>
public enum WorkItemDifficulty
{
    VeryEasy = 0,
    Easy     = 1,
    Medium   = 2,
    Hard     = 3,
    VeryHard = 4
}

/// <summary>Tempo estimado para completar um work item.</summary>
public enum WorkItemEstimatedTime
{
    LessThan1Hour = 0,
    OneToTwoHours = 1,
    HalfDay       = 2,
    OneDay        = 3,
    TwoToThreeDays = 4,
    MoreThanThreeDays = 5
}
