using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

/// <summary>
/// Registo imutável de uma transição de estado de projeto.
/// NÃO herda BaseEntity — não deve ser atualizado ou apagado.
/// Reason é obrigatório quando ToState é Paused ou Cancelled (validado por FluentValidation).
/// </summary>
public class StateTransition
{
    public Guid         Id               { get; set; } = Guid.NewGuid();
    public Guid         ProjectId        { get; set; }
    public ProjectState FromState        { get; set; }
    public ProjectState ToState          { get; set; }
    public string?      Reason           { get; set; }
    public Guid         ChangedByUserId  { get; set; }
    public DateTime     ChangedAt        { get; set; } = DateTime.UtcNow;

    // Navegação
    public Project Project          { get; set; } = null!;
    public User    ChangedByUser    { get; set; } = null!;
}
