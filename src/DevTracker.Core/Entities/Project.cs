using DevTracker.Core.Entities.Base;
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

/// <summary>
/// Projeto principal. Tem uma pasta fisica no WorkspaceRoot gerida por IWorkspaceService.
/// Soft delete: IsDeleted=true + DeletedAt. Para ver projetos apagados usar .IgnoreQueryFilters().
/// </summary>
public class Project : BaseEntity
{
    public string       Name          { get; set; } = string.Empty;
    public string?      Description   { get; set; }
    /// <summary>Cor em hex (#RRGGBB) usada na UI para identificacao visual.</summary>
    public string       Color         { get; set; } = "#2563EB";
    /// <summary>Caminho absoluto da pasta do projeto no disco, dentro do WorkspaceRoot.</summary>
    public string       WorkspacePath { get; set; } = string.Empty;
    /// <summary>URL do repositorio GitHub para integracao com controlo de versoes.</summary>
    public string?      GitHubRepositoryUrl { get; set; }
    public ProjectState State         { get; set; } = ProjectState.NotStarted;
    public bool         IsDeleted     { get; set; } = false;
    public DateTime?    DeletedAt     { get; set; }
    public string?      UsedAiName    { get; set; }

    // Navegacao
    public ICollection<Repository>       Repositories     { get; set; } = [];
    public ICollection<WorkItem>         WorkItems        { get; set; } = [];
    public ICollection<StateTransition>  StateTransitions { get; set; } = [];
}
