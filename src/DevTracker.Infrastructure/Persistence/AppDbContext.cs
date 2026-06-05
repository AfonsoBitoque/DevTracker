using DevTracker.Core.Entities;
using DevTracker.Core.Entities.Base;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core principal. Aplica configurações por assembly e atualiza UpdatedAt
/// automaticamente via override de SaveChanges (sem necessidade de gestão manual).
/// Enums são guardados como string (ver configurações individuais).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>            Users            => Set<User>();
    public DbSet<Project>         Projects         => Set<Project>();
    public DbSet<Repository>      Repositories     => Set<Repository>();
    public DbSet<WorkItem>        WorkItems        => Set<WorkItem>();
    public DbSet<Comment>         Comments         => Set<Comment>();
    public DbSet<Label>           Labels           => Set<Label>();
    public DbSet<WorkItemLabel>   WorkItemLabels   => Set<WorkItemLabel>();
    public DbSet<StateTransition> StateTransitions => Set<StateTransition>();
    public DbSet<AuditEntry>      AuditEntries     => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(ct);
    }

    /// <summary>Atualiza UpdatedAt em todas as entidades modificadas que herdam BaseEntity.</summary>
    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = DateTime.UtcNow;
    }
}
