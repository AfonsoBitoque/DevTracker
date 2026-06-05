# DevTracker — Domain Model & EF Core Context

> Ficheiro de contexto: entidades, relações, DbContext e configuração EF Core.
> Usar em conjunto com STACK.md.

---

## 1. Enums

```csharp
// src/DevTracker.Core/Enums/UserRole.cs
namespace DevTracker.Core.Enums;

public enum UserRole
{
    Owner       = 0,
    Admin       = 1,
    Maintainer  = 2,
    Reader      = 3
}
```

```csharp
// src/DevTracker.Core/Enums/ProjectState.cs
namespace DevTracker.Core.Enums;

public enum ProjectState
{
    NotStarted  = 0,
    InProgress  = 1,
    Paused      = 2,
    Cancelled   = 3,
    Completed   = 4
}
```

```csharp
// src/DevTracker.Core/Enums/WorkItemType.cs
namespace DevTracker.Core.Enums;

public enum WorkItemType
{
    Task            = 0,
    Feature         = 1,
    Bug             = 2,
    Improvement     = 3,
    Documentation   = 4
}
```

```csharp
// src/DevTracker.Core/Enums/WorkItemStatus.cs
namespace DevTracker.Core.Enums;

public enum WorkItemStatus
{
    Backlog     = 0,
    Todo        = 1,
    InProgress  = 2,
    InReview    = 3,
    Done        = 4,
    Cancelled   = 5
}
```

```csharp
// src/DevTracker.Core/Enums/WorkItemPriority.cs
namespace DevTracker.Core.Enums;

public enum WorkItemPriority
{
    Low      = 0,
    Medium   = 1,
    High     = 2,
    Critical = 3
}
```

```csharp
// src/DevTracker.Core/Enums/AuditAction.cs
namespace DevTracker.Core.Enums;

public enum AuditAction
{
    // Auth
    Login               = 100,
    Logout              = 101,
    PasswordChanged     = 102,
    // Projects
    ProjectCreated      = 200,
    ProjectUpdated      = 201,
    ProjectStateChanged = 202,
    ProjectDeleted      = 203,
    // Repositories
    RepositoryCreated   = 300,
    RepositoryRenamed   = 301,
    RepositoryDeleted   = 302,
    // Work Items
    WorkItemCreated     = 400,
    WorkItemUpdated     = 401,
    WorkItemStatusChanged = 402,
    WorkItemDeleted     = 403,
    // Users
    UserCreated         = 500,
    UserRoleChanged     = 501,
    UserDeleted         = 502,
}
```

---

## 2. Base Entity

```csharp
// src/DevTracker.Core/Entities/Base/BaseEntity.cs
namespace DevTracker.Core.Entities.Base;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

> **Nota sobre `UpdatedAt`:** O valor `= DateTime.UtcNow` no campo é o valor de inicialização C# do objeto, não do momento de persistência.
> A atualização real do `UpdatedAt` em cada `SaveChanges` é feita pelo override em `AppDbContext.UpdateTimestamps()`.
> Este mecanismo garante que `UpdatedAt` reflete sempre o momento da persistência, independentemente de quando o objeto foi criado em memória.

---

## 3. Entidades

### User

```csharp
// src/DevTracker.Core/Entities/User.cs
using DevTracker.Core.Entities.Base;
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;

    /// <summary>Argon2id hash da password. Nunca guardar plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Salt único gerado por utilizador.</summary>
    public string Salt { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Reader;

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Navegação
    public ICollection<WorkItem> AssignedWorkItems { get; set; } = [];
    public ICollection<WorkItem> CreatedWorkItems { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<StateTransition> StateTransitions { get; set; } = [];
    public ICollection<AuditEntry> AuditEntries { get; set; } = [];
}
```

### Project

```csharp
// src/DevTracker.Core/Entities/Project.cs
using DevTracker.Core.Entities.Base;
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Cor hex para identificação visual. Ex: "#4f98a3"</summary>
    public string Color { get; set; } = "#4f98a3";

    /// <summary>Caminho absoluto para a pasta do projeto no disco.</summary>
    public string WorkspacePath { get; set; } = string.Empty;

    public ProjectState State { get; set; } = ProjectState.NotStarted;

    /// <summary>Soft delete: projetos deletados ficam no registo histórico.</summary>
    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    // Navegação
    public ICollection<Repository> Repositories { get; set; } = [];
    public ICollection<WorkItem> WorkItems { get; set; } = [];
    public ICollection<StateTransition> StateTransitions { get; set; } = [];
}
```

### StateTransition

> **Nota de design:** `StateTransition` não herda `BaseEntity` intencionalmente.
> É um registo de evento imutável (só INSERT). Não tem `UpdatedAt` porque nunca é modificado.
> `CreatedAt` seria redundante com `ChangedAt`. Manter simples e explícito.

```csharp
// src/DevTracker.Core/Entities/StateTransition.cs
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

public class StateTransition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public ProjectState FromState { get; set; }
    public ProjectState ToState { get; set; }

    /// <summary>Razão obrigatória para transições para Paused ou Cancelled.</summary>
    public string? Reason { get; set; }

    public Guid ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
```

### Repository

```csharp
// src/DevTracker.Core/Entities/Repository.cs
using DevTracker.Core.Entities.Base;

namespace DevTracker.Core.Entities;

public class Repository : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Caminho relativo ao WorkspacePath do projeto. Ex: "repo-backend"</summary>
    public string RelativePath { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
```

### WorkItem

```csharp
// src/DevTracker.Core/Entities/WorkItem.cs
using DevTracker.Core.Entities.Base;
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

public class WorkItem : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>Número sequencial dentro do projeto. Ex: #42</summary>
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public WorkItemType Type { get; set; } = WorkItemType.Task;
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Backlog;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    public DateTime? DueDate { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navegação
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<WorkItemLabel> WorkItemLabels { get; set; } = [];
}
```

### Comment

```csharp
// src/DevTracker.Core/Entities/Comment.cs
namespace DevTracker.Core.Entities;

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public User Author { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
}
```

### Label

```csharp
// src/DevTracker.Core/Entities/Label.cs
namespace DevTracker.Core.Entities;

public class Label
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Cor hex. Ex: "#da7101"</summary>
    public string Color { get; set; } = "#7a7974";

    public ICollection<WorkItemLabel> WorkItemLabels { get; set; } = [];
}
```

### WorkItemLabel (tabela de junção)

```csharp
// src/DevTracker.Core/Entities/WorkItemLabel.cs
namespace DevTracker.Core.Entities;

public class WorkItemLabel
{
    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public Guid LabelId { get; set; }
    public Label Label { get; set; } = null!;
}
```

### AuditEntry

```csharp
// src/DevTracker.Core/Entities/AuditEntry.cs
using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

/// <summary>
/// Registo imutável de todas as operações sensíveis.
/// NUNCA fazer UPDATE ou DELETE nesta tabela.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public AuditAction Action { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>JSON serializado do estado anterior (nullable).</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON serializado do novo estado.</summary>
    public string? NewValue { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>IP ou hostname da máquina (para auditoria forense).</summary>
    public string? MachineName { get; set; }
}
```

---

## 4. AppDbContext

```csharp
// src/DevTracker.Infrastructure/Persistence/AppDbContext.cs
using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<WorkItemLabel> WorkItemLabels => Set<WorkItemLabel>();
    public DbSet<StateTransition> StateTransitions => Set<StateTransition>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar todas as configurações da pasta Configurations/
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// Atualiza automaticamente UpdatedAt em entidades BaseEntity antes de guardar.
    /// </summary>
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

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Core.Entities.Base.BaseEntity
                        && e.State == EntityState.Modified);

        foreach (var entry in entries)
            ((Core.Entities.Base.BaseEntity)entry.Entity).UpdatedAt = DateTime.UtcNow;
    }
}
```

---

## 5. Configurações EF Core (Fluent API)

### UserConfiguration

```csharp
// src/DevTracker.Infrastructure/Persistence/Configurations/UserConfiguration.cs
using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(u => u.Salt).IsRequired().HasMaxLength(256);
        builder.Property(u => u.Role).HasConversion<string>();

        // Relações: evitar delete em cascata para WorkItems (preservar histórico)
        builder.HasMany(u => u.AssignedWorkItems)
            .WithOne(w => w.AssignedToUser)
            .HasForeignKey(w => w.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.CreatedWorkItems)
            .WithOne(w => w.CreatedByUser)
            .HasForeignKey(w => w.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### ProjectConfiguration

```csharp
// src/DevTracker.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs
using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Color).IsRequired().HasMaxLength(7);
        builder.Property(p => p.WorkspacePath).IsRequired().HasMaxLength(512);
        builder.Property(p => p.State).HasConversion<string>();

        // Filtro global: excluir projetos soft-deleted por padrão
        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasMany(p => p.Repositories)
            .WithOne(r => r.Project)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.WorkItems)
            .WithOne(w => w.Project)
            .HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.StateTransitions)
            .WithOne(st => st.Project)
            .HasForeignKey(st => st.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### WorkItemConfiguration

```csharp
// src/DevTracker.Infrastructure/Persistence/Configurations/WorkItemConfiguration.cs
using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Title).IsRequired().HasMaxLength(256);
        builder.Property(w => w.Type).HasConversion<string>();
        builder.Property(w => w.Status).HasConversion<string>();
        builder.Property(w => w.Priority).HasConversion<string>();

        // Index para busca por projeto + número
        builder.HasIndex(w => new { w.ProjectId, w.Number }).IsUnique();

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
```

### CommentConfiguration

```csharp
// src/DevTracker.Infrastructure/Persistence/Configurations/CommentConfiguration.cs
using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Body).IsRequired().HasMaxLength(4096);

        // Soft delete: comentários apagados não aparecem nas queries por defeito
        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasOne(c => c.Author)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### WorkItemLabelConfiguration

```csharp
// src/DevTracker.Infrastructure/Persistence/Configurations/WorkItemLabelConfiguration.cs
using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public class WorkItemLabelConfiguration : IEntityTypeConfiguration<WorkItemLabel>
{
    public void Configure(EntityTypeBuilder<WorkItemLabel> builder)
    {
        builder.HasKey(wl => new { wl.WorkItemId, wl.LabelId });

        builder.HasOne(wl => wl.WorkItem)
            .WithMany(w => w.WorkItemLabels)
            .HasForeignKey(wl => wl.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wl => wl.Label)
            .WithMany(l => l.WorkItemLabels)
            .HasForeignKey(wl => wl.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### AuditEntryConfiguration

```csharp
// src/DevTracker.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs
using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<string>();
        builder.Property(a => a.EntityType).HasMaxLength(128);
        builder.Property(a => a.MachineName).HasMaxLength(128);

        // Relação com User: se user for deletado, manter o registo (SetNull)
        builder.HasOne(a => a.User)
            .WithMany(u => u.AuditEntries)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Índice para queries por utilizador + data
        builder.HasIndex(a => new { a.UserId, a.Timestamp });
        builder.HasIndex(a => a.Timestamp);
    }
}
```

---

## 6. Registo do DbContext (Infrastructure DI)

```csharp
// src/DevTracker.Infrastructure/DependencyInjection.cs
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string dbPath)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}",
                sqlite => sqlite.MigrationsAssembly(
                    typeof(AppDbContext).Assembly.FullName)));

        return services;
    }
}
```

---

## 7. Inicialização e Migrações em Runtime

```csharp
// src/DevTracker.Desktop/Program.cs (trecho relevante)
// Aplicar migrações pendentes automaticamente ao arrancar a app
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.MigrateAsync();
// Se for a primeira execução (sem utilizadores), criar o Owner padrão
// → Ver AUTH.md para o fluxo de setup inicial
```

---

## 8. Notas Importantes

1. **Enums guardados como string** (`HasConversion<string>()`) — torna o SQLite legível sem necessidade de decoder.
2. **Soft delete com QueryFilter global** — `Project`, `WorkItem`, `Repository` e `Comment` têm soft delete com `QueryFilter`. Para ver registos deletados: `.IgnoreQueryFilters()`.
3. **Audit log**: nunca incluir `AuditEntry` nas migrações com `DeleteBehavior.Cascade`. O registo de auditoria deve sobreviver à eliminação do utilizador (SetNull).
4. **WorkItem.Number**: gerado no serviço de aplicação como `MAX(Number) + 1` por projeto usando `.IgnoreQueryFilters()`, não pelo EF/SQLite, para garantir sequência por projeto (ver SERVICES.md).
5. **StateTransition.Reason**: obrigatório quando `ToState` é `Paused` ou `Cancelled` — validação feita em `FluentValidation`, não no modelo.
6. **StateTransition não herda BaseEntity** — é um registo de evento imutável. `ChangedAt` substitui `CreatedAt`. Nunca fazer UPDATE nesta tabela.
7. **UpdatedAt em BaseEntity**: o valor default `= DateTime.UtcNow` define o valor de criação do objeto C#. A atualização automática em cada persistência é garantida pelo override `AppDbContext.UpdateTimestamps()`.

