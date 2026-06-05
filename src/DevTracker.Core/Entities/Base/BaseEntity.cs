namespace DevTracker.Core.Entities.Base;

/// <summary>
/// Base comum para todas as entidades com identidade própria.
/// UpdatedAt é atualizado automaticamente por AppDbContext.SaveChanges()
/// via override de UpdateTimestamps() — não precisa de ser gerido manualmente.
/// </summary>
public abstract class BaseEntity
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
