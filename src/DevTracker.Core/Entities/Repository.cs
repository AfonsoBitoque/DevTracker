using DevTracker.Core.Entities.Base;

namespace DevTracker.Core.Entities;

/// <summary>
/// Repositório de código associado a um projeto.
/// RelativePath é relativo ao WorkspacePath do projeto pai.
/// Soft delete: IsDeleted=true + DeletedAt.
/// </summary>
public class Repository : BaseEntity
{
    public Guid     ProjectId    { get; set; }
    public string   Name         { get; set; } = string.Empty;
    /// <summary>Caminho relativo ao WorkspacePath do projeto (ex: "backend", "frontend").</summary>
    public string   RelativePath { get; set; } = string.Empty;
    public string?  Description  { get; set; }
    public bool     IsDeleted    { get; set; } = false;
    public DateTime? DeletedAt   { get; set; }

    // Navegação
    public Project Project { get; set; } = null!;
}
