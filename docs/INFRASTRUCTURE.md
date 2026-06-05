# DevTracker.Infrastructure - Análise Técnica

Camada de infraestrutura. Implementa todas as interfaces de Application usando EF Core, SQLite, Argon2id, e operações de filesystem.

---

## Persistence

### `AppDbContext.cs`
**Responsabilidade**: Contexto EF Core principal.

```csharp
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
}
```

**Padrões**:
- `OnModelCreating`: Aplica configurações por assembly (`ApplyConfigurationsFromAssembly`)
- `SaveChanges override`: Atualiza `UpdatedAt` automaticamente em entidades `BaseEntity` modificadas
- Enums guardados como string via `HasConversion<string>()`

### Configurações EF Core

#### `WorkItemConfiguration.cs`
```csharp
builder.HasKey(w => w.Id);
builder.Property(w => w.Title).IsRequired().HasMaxLength(256);
builder.Property(w => w.Type).HasConversion<string>();
builder.Property(w => w.Status).HasConversion<string>();
builder.Property(w => w.Priority).HasConversion<string>();
builder.Property(w => w.Difficulty).HasConversion<string>();
builder.Property(w => w.EstimatedTime).HasConversion<string>();
builder.HasIndex(w => new { w.ProjectId, w.Number }).IsUnique();
builder.HasQueryFilter(w => !w.IsDeleted);
```

#### `UserConfiguration.cs`
```csharp
builder.HasKey(u => u.Id);
builder.Property(u => u.Username).IsRequired().HasMaxLength(64);
builder.HasIndex(u => u.Username).IsUnique();
builder.Property(u => u.Role).HasConversion<string>();
builder.HasMany(u => u.CreatedWorkItems)
       .WithOne(w => w.CreatedByUser)
       .HasForeignKey(w => w.CreatedByUserId)
       .OnDelete(DeleteBehavior.Restrict);  // Não apagar user com work items
builder.HasMany(u => u.AssignedWorkItems)
       .WithOne(w => w.AssignedToUser)
       .HasForeignKey(w => w.AssignedToUserId)
       .OnDelete(DeleteBehavior.SetNull);   // Se apagar user, fica sem atribuição
```

#### `ProjectConfiguration.cs`
```csharp
builder.HasKey(p => p.Id);
builder.Property(p => p.Name).IsRequired().HasMaxLength(128);
builder.Property(p => p.Color).IsRequired().HasMaxLength(7);
builder.Property(p => p.State).HasConversion<string>();
builder.HasQueryFilter(p => !p.IsDeleted);
```

#### `OtherEntityConfigurations.cs`
Agrupa configurações de entidades com setup simples:

| Configuração | Chave | Conversões | Relações |
|--------------|-------|-----------|----------|
| Repository | HasKey, Name max 128 | — | QueryFilter soft delete |
| Comment | HasKey | — | QueryFilter soft delete; Author → Restrict |
| Label | HasKey, Name max 64, Color max 7 | — | — |
| WorkItemLabel | Chave composta (WorkItemId, LabelId) | — | — |
| StateTransition | HasKey | FromState/ToState → string | — |
| AuditEntry | HasKey, EntityType max 128, índices Timestamp e UserId+Timestamp | Action → string | User → SetNull |

### `DesignTimeDbContextFactory.cs`
**Responsabilidade**: Factory para criar DbContext em design time (migrations).

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=devtracker_design.db");
        return new AppDbContext(optionsBuilder.Options);
    }
}
```

---

## Security Implementations

### `AuthService.cs`
**Implementa**: `IAuthService`, `IReAuthenticationService`

**Dependências**: `AppDbContext`, `IPasswordHasher`, `ISessionService`, `IAuditService`

**Métodos**:

| Método | Fluxo |
|--------|-------|
| `SetupFirstOwnerAsync` | Verifica `AnyAsync()` users → cria Owner → inicia sessão → audit UserCreated + Login |
| `LoginAsync` | Procura user ativo → verifica password → atualiza LastLoginAt → inicia sessão → audit Login |
| `LogoutAsync` | Termina sessão → audit Logout |
| `ChangePasswordAsync` | Verifica current password → gera novo hash → save → audit PasswordChanged |
| `ConfirmPasswordAsync` | Valida password para re-autenticação |

**Sessão**: Duração fixa de 8h (`TimeSpan.FromHours(8)`). Token único por sessão (`Guid.NewGuid()`).

### `Argon2PasswordHasher.cs`
**Implementa**: `IPasswordHasher`

**Biblioteca**: `Isopoh.Cryptography.Argon2`

**Configuração**:
```csharp
private const int SaltLength  = 32;
private const int HashLength  = 32;
private const int TimeCost    = 4;
private const int MemoryCost  = 65536;  // KB
private const int Parallelism = 4;
```

**Algoritmo**: Argon2id (HybridAddressing), versão 19

**Segurança**:
- Salt aleatório 32 bytes por cada `Hash()`
- Comparação constant-time via XOR para evitar timing attacks:
```csharp
private static bool CryptographicEquals(string a, string b)
{
    if (a.Length != b.Length) return false;
    var diff = 0;
    for (var i = 0; i < a.Length; i++)
        diff |= a[i] ^ b[i];
    return diff == 0;
}
```

### `InMemorySessionService.cs`
**Implementa**: `ISessionService`

**Lifetime**: Singleton

**Timeouts**:
```csharp
private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);
private static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);
```

**Lógica de expiração**:
```csharp
public bool IsExpired()
{
    if (_session is null) return true;
    var now = DateTime.UtcNow;
    return now > _session.ExpiresAtUtc
        || now - _session.LastActivityUtc > IdleTimeout;
}
```

**Persistência**: Nunca persistida em disco. `_session` é um campo privado.

### `PermissionService.cs`
**Implementa**: `IPermissionService`

**Matriz RBAC** (Dictionary estático `UserRole → HashSet<string>`):

| Role | Permissões |
|------|-----------|
| **Owner** | **Todas** |
| **Admin** | Todas exceto: `project.delete`, `user.create/update/delete/change-role` |
| **Maintainer** | `project.read/update/change-state`, `repo.*`, `workitem.*` (exceto delete), `user.read`, `security.*`, `settings.read` |
| **Reader** | `project.read`, `repo.read`, `workitem.read/comment`, `security.*`, `settings.read` |

**Métodos**:
- `CanPerformAsync`: 1 query à BD (`AsNoTracking`) para obter role do utilizador → lookup na matriz
- `EnsureCanPerformAsync`: Chama `CanPerformAsync` → lança `AuthorizationException` se false

### `PermissionSnapshotService.cs`
**Implementa**: `IPermissionSnapshotService`

**Cache**: `HashSet<string>? _cached` + `Guid? _cachedForUserId`

**Lógica**:
```csharp
if (_cached is not null && _cachedForUserId == userId)
    return Task.FromResult(_cached);  // Cache hit

// Cache miss: lookup na matriz estática
_cached = Matrix.TryGetValue(currentUser.Role.Value, out var perms)
    ? new HashSet<string>(perms)
    : [];
_cachedForUserId = userId;
```

**Observação**: Usa `PermissionService.GetMatrix()` para evitar duplicação da matriz.

### `CriticalActionTokenService.cs`
**Implementa**: `ICriticalActionTokenService`

**Storage**: `ConcurrentDictionary<Guid, TokenEntry> _tokens`

**Validade**: 30 segundos (`TimeSpan.FromSeconds(30)`)

**Métodos**:
- `CreateToken`: Gera Guid → adiciona ao dicionário com UserId, Permission e ExpiresAt
- `ConsumeToken`: `TryRemove` → verifica UserId, Permission e expiração → retorna bool
- `PurgeExpired`: Remove tokens expirados antes de criar novo

### `CurrentUserService.cs`
**Implementa**: `ICurrentUserService`

**Lifetime**: Singleton

**Derivado de**: `ISessionService.Current`
```csharp
public Guid?    UserId          => session.Current?.UserId;
public string?  Username        => session.Current?.Username;
public UserRole? Role           => session.Current?.Role;
public bool     IsAuthenticated => session.IsAuthenticated;
```

---

## Services Implementations

### `ProjectService.cs`
**Implementa**: `IProjectService`

**Dependências**: `AppDbContext`, `ICurrentUserService`, `IPermissionService`, `IAuditService`, `IWorkspaceService`

**Fluxo padrão**: Verificar auth → `EnsureCanPerformAsync` → validar DTO → lógica de negócio → audit

**Métodos**:

| Método | Lógica |
|--------|--------|
| `GetAllAsync` | `AsNoTracking` → projeção para `ProjectSummaryDto` com counts |
| `GetByIdAsync` | `AsNoTracking` + `Include(Repositories, WorkItems, AssignedToUser)` |
| `CreateAsync` | Valida → cria pasta via `WorkspaceService` → adiciona Project → audit |
| `UpdateAsync` | Verifica duplicados por nome → atualiza → audit |
| `ChangeStateAsync` | Valida state → cria `StateTransition` → atualiza estado → audit |
| `ArchiveAsync` | Soft delete (`IsDeleted=true, DeletedAt=UtcNow`) |
| `DeleteAsync` | Delete físico com `.IgnoreQueryFilters()` |

### `WorkItemService.cs`
**Implementa**: `IWorkItemService`

**Dependências**: `AppDbContext`, `ICurrentUserService`, `IPermissionService`, `IAuditService`

**Regra crítica — geração de Number**:
```csharp
var nextNumber = (await db.WorkItems.IgnoreQueryFilters()
    .Where(w => w.ProjectId == request.ProjectId)
    .MaxAsync(w => (int?)w.Number, ct) ?? 0) + 1;
```

**Razão**: `.IgnoreQueryFilters()` contabiliza soft-deleted para evitar reutilização de números.

### `UserService.cs`
**Implementa**: `IUserService`

**Dependências**: `AppDbContext`, `ICurrentUserService`, `IPermissionService`, `IPasswordHasher`, `IAuditService`

**Regras críticas**:

| Regra | Implementação |
|-------|-------------|
| Último Owner ativo | `HasOtherActiveOwnerAsync(excludeUserId)` — verifica `CountAsync` |
| Admin não promove a Owner | `if (request.NewRole == Owner && currentUser.Role != Owner) return Fail` |
| Owner só via bootstrap | `CreateUserRequestValidator` rejeita `Role == Owner` |

**Métodos**:
- `CreateAsync`: Hash password → cria User → audit
- `ChangeRoleAsync`: Verifica regras → atualiza → audit
- `DeactivateAsync`: Verifica último Owner → `IsActive=false` → audit
- `DeleteAsync`: Verifica último Owner → remove → audit

### `AuditService.cs`
**Implementa**: `IAuditService`

**Dependência**: `AppDbContext`

**LogAsync**:
```csharp
db.AuditEntries.Add(new AuditEntry
{
    UserId = userId, Action = action, EntityType = entityType,
    EntityId = entityId, OldValue = oldValue, NewValue = newValue
});
await db.SaveChangesAsync(ct);
```

**Queries**: `BaseQuery()` usa `AsNoTracking()` + `Include(a => a.User)`

### `RepositoryService.cs`
**Implementa**: `IRepositoryService`

**Dependências**: `AppDbContext`, `ICurrentUserService`, `IPermissionService`, `IAuditService`, `IWorkspaceService`

**CreateAsync**: Cria pasta física → calcula `RelativePath = Path.GetRelativePath(project.WorkspacePath, folderPath)`
**AttachAsync**: Move/copia pasta externa → calcula RelativePath

### `DashboardService.cs`
**Implementa**: `IDashboardService`

**Dependências**: `AppDbContext`, `ICurrentUserService`

**Métricas calculadas**:
- **Projetos**: Total, Ativos (`NotStarted || InProgress`)
- **Tarefas**: Concluídas (`Done`), Por Fazer (`Backlog || Todo`), Em Curso (`InProgress`)
- **Horas estimadas**: Soma de `ConvertToHours(EstimatedTime)` para tarefas por fazer

**Conversão de tempo**:
```csharp
LessThan1Hour      => 0.5
OneToTwoHours      => 1.5
HalfDay            => 4
OneDay             => 8
TwoToThreeDays     => 20
MoreThanThreeDays  => 40
```

---

## IO

### `AppPaths.cs`
**Implementa**: `IAppPaths`

**Lifetime**: Singleton imutável

**Construção**:
```csharp
AppDataRoot   = Path.Combine(local, "DevTracker");
DatabasePath  = Path.Combine(AppDataRoot, "data.db");
LogsPath      = Path.Combine(AppDataRoot, "logs");
ConfigPath    = Path.Combine(AppDataRoot, "config.json");
WorkspaceRoot = Path.GetFullPath(workspaceRoot);
```

**Método estático** `ReadWorkspaceRootFromConfig()`: Leitura síncrona de `config.json` antes do DI estar disponível.

### `WorkspaceService.cs`
**Implementa**: `IWorkspaceService`

**Dependências**: `IPermissionService`, `ICurrentUserService`, `IAuditService`, `IAppPaths`

**Segurança**: `IsInsideWorkspace` verifica prefixo do path normalizado.

**AttachExistingFolderAsync**: Tenta `Directory.Move` (mesma partição); fallback para `CopyDirectoryRecursive` + `Directory.Delete` (cross-volume).

**ListEntriesAsync**: Lista ficheiros e pastas de forma read-only, retornando `IReadOnlyList<WorkspaceEntry>`. Retorna empty se path inválido ou fora do workspace.

**CopyDirectoryContentsAsync**: Copia conteúdo recursivamente de origem para destino com validação de workspace boundary. Usa `File.Copy` (cross-device safe, evita `Directory.Move` entre volumes diferentes).

**Archive**: Move para `_archive/{nome}-{yyyyMMdd-HHmmss}`.

### `FileNameSanitizer.cs`
**Responsabilidade**: Sanitiza nomes de pasta.

```csharp
public static string SanitizeFolderName(string input)
{
    var trimmed = input.Trim();
    trimmed = trimmed.Replace("..", "-");
    trimmed = InvalidCharsRegex().Replace(trimmed, "-");
    trimmed = Regex.Replace(trimmed, @"\s+", " ");
    return trimmed.Length > 80 ? trimmed[..80].Trim() : trimmed;
}
```

**Regex**: `[^a-zA-Z0-9._ -]` — remove tudo exceto alfanumérico, ponto, underscore, hífen e espaço.

---

## Settings

### `SettingsService.cs`
**Implementa**: `ISettingsService`

**Lifetime**: Singleton

**Configuração JSON**:
```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

**Resiliência**: Ficheiro corrompido/inexistente → silenciar e usar `new AppSettings()`.

---

## Git Services

### `GitService.cs`
**Responsabilidade**: Operações Git locais via CLI.

**Mecanismo**: `ProcessStartInfo` com `FileName="git"`, `RedirectStandardOutput/Error=true`.

**Métodos principais**:
- `CloneAsync`: Converte SSH para HTTPS; suporta token de autenticação
- `InitAsync`: `git init` + configura user.name
- `CreateBranchAsync`: Sanitiza nome da branch (remove chars inválidos)
- `GetDiffAsync`: `git diff`
- `AddRemoteAsync`: Converte SSH para HTTPS; adiciona token à URL
- `PushAsync`: Tenta push normal → fallback para force push
- `PullAsync`: Configura `pull.rebase false` + `--allow-unrelated-histories`

**SSH**: Configura `GIT_SSH_COMMAND="ssh -o StrictHostKeyChecking=no"` para evitar bloqueio.

### `GitHubService.cs`
**Responsabilidade**: Integração com GitHub API.

**Dependência**: `HttpClient`

**Métodos**:
- `RepositoryExistsAsync`: GET `/repos/{owner}/{repo}`
- `CreateRepositoryAsync`: POST `/user/repos` com payload JSON
- `GetCloneUrl`: Converte HTTPS para SSH (`git@github.com:...`)

---

## DependencyInjection

### `DependencyInjection.cs`
**Responsabilidade**: Registo de todos os serviços da Infrastructure.

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath)
{
    // EF Core SQLite
    services.AddDbContext<AppDbContext>(opts => opts.UseSqlite($"Data Source={databasePath}"));
    
    // Singletons — estado global
    services.AddSingleton<ISessionService, InMemorySessionService>();
    services.AddSingleton<ICurrentUserService, CurrentUserService>();
    services.AddSingleton<ICriticalActionTokenService, CriticalActionTokenService>();
    
    // Scoped — ciclo de vida por operação
    services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
    services.AddScoped<IPermissionService, PermissionService>();
    services.AddScoped<IPermissionSnapshotService, PermissionSnapshotService>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IReAuthenticationService>(sp => (AuthService)sp.GetRequiredService<IAuthService>());
    
    // Serviços de domínio
    services.AddScoped<IAuditService, AuditService>();
    services.AddScoped<IProjectService, ProjectService>();
    services.AddScoped<IWorkItemService, WorkItemService>();
    services.AddScoped<IRepositoryService, RepositoryService>();
    services.AddScoped<IUserService, UserService>();
    services.AddScoped<IDashboardService, DashboardService>();
    
    // IO & Settings
    services.AddScoped<IWorkspaceService, WorkspaceService>();
    services.AddSingleton<ISettingsService, SettingsService>();
    
    return services;
}
```

**EnsureDatabase**:
```csharp
public static void EnsureDatabase(IServiceProvider services)
{
    using var scope = services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}
```

**Observação**: Usa `EnsureCreated` (não `Migrate`) — não há ficheiros de migration EF gerados.
