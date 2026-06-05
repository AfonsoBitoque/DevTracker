# DevTracker.Application - Análise Técnica

Camada de aplicação. Contém abstrações (interfaces), DTOs, validadores, settings e padrões comuns. Sem dependências de infraestrutura.

---

## Abstractions (Interfaces)

### Segurança

#### `IAuthService.cs`
**Responsabilidade**: Gestão do ciclo de vida de autenticação local.

**Records**:
```csharp
public sealed record AuthResult(bool Succeeded, string? Error = null, SessionInfo? Session = null);
public sealed record SessionInfo(Guid UserId, string Username, string Role, bool IsLocked);
```

**Métodos**:
| Método | Descrição |
|--------|-----------|
| `SetupFirstOwnerAsync` | Cria o primeiro Owner. Falha se já existirem utilizadores. |
| `LoginAsync` | Autentica utilizador, inicia sessão, faz audit. |
| `LogoutAsync` | Termina sessão, faz audit. |
| `ChangePasswordAsync` | Altera password do utilizador autenticado. |
| `AnyUsersExistAsync` | Verifica se existem utilizadores na BD. |

#### `ISessionService.cs`
**Responsabilidade**: Gestão da sessão ativa em memória. Singleton.

**Record**:
```csharp
public sealed record UserSession(
    Guid Token, Guid UserId, string Username, UserRole Role,
    DateTime StartedAtUtc, DateTime ExpiresAtUtc, DateTime LastActivityUtc);
```

**Propriedades**: Current, IsAuthenticated, IsLocked
**Timeouts**: Inatividade 15min, expiração dura 8h
**Métodos**: Start, End, Touch (atualiza LastActivityUtc), Lock, IsExpired

**Regra**: A sessão nunca é persistida em disco.

#### `ICurrentUserService.cs`
**Responsabilidade**: Acesso ao utilizador autenticado atual. Singleton, derivado de ISessionService.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| UserId | Guid? | Null se não autenticado |
| Username | string? | Null se não autenticado |
| Role | UserRole? | Null se não autenticado |
| IsAuthenticated | bool | Deriva de session.IsAuthenticated |

#### `IPermissionService.cs`
**Responsabilidade**: Autorização centralizada baseada em matriz estática Role → permissões.

**Padrão**: Nunca usar `if (role == ...)` no código — delegar sempre a este serviço.

| Método | Retorno | Descrição |
|--------|---------|-----------|
| `CanPerformAsync(userId, permission)` | `Task<bool>` | Para UI adaptativa |
| `EnsureCanPerformAsync(userId, permission)` | `Task` | Lança AuthorizationException se não autorizado |

**Regra**: `AuthorizationException` é a ÚNICA exceção permitida para falhas de autorização.

#### `IPermissionSnapshotService.cs`
**Responsabilidade**: Cache de permissões por instância (Scoped).

| Método | Retorno | Descrição |
|--------|---------|-----------|
| `GetCurrentUserPermissionsAsync()` | `Task<HashSet<string>>` | Cache em memória por instância |
| `InvalidateCache()` | `void` | Força nova leitura na próxima chamada |

**Regra**: Obrigatório nos ViewModels para evitar N queries por ecrã. NÃO substitui IPermissionService nos serviços.

#### `IPasswordHasher.cs`
**Responsabilidade**: Hashing de passwords com Argon2id.

**Record**: `PasswordHashResult(string Hash, string Salt)`

**Configuração**: TimeCost=4, MemoryCost=65536 KB, Lanes=4, HashLength=32 bytes

| Método | Descrição |
|--------|-----------|
| `Hash(password)` | Gera novo hash com salt aleatório de 32 bytes |
| `Verify(password, hash, salt)` | Verifica se a password corresponde ao hash+salt |

#### `IReAuthenticationService.cs`
**Responsabilidade**: Re-autenticação para operações destrutivas.

| Método | Descrição |
|--------|-----------|
| `ConfirmPasswordAsync(userId, password)` | Verifica password atual do utilizador |

**Fluxo**: UI recolhe password → ConfirmPasswordAsync → se sucesso, executar operação.

#### `ICriticalActionTokenService.cs`
**Responsabilidade**: Tokens de ação crítica single-use, 30s de validade, 100% em memória.

**Fluxo**: UI faz re-auth → CreateToken → passa Guid ao serviço → ConsumeToken. Evita passar passwords em plaintext.

| Método | Descrição |
|--------|-----------|
| `CreateToken(userId, permission)` | Cria token válido por 30s |
| `ConsumeToken(token, userId, permission)` | Consome e invalida o token |

### Navegação

#### `INavigationService.cs`
**Responsabilidade**: Navegação entre páginas via substituição do CurrentPage no ShellViewModel.

| Método | Descrição |
|--------|-----------|
| `NavigateTo<TViewModel>()` | Navega para nova página |
| `NavigateTo<TViewModel>(configure)` | Navega com configuração prévia |
| `NavigateBack()` | Volta à página anterior |
| `CanNavigateBack` | `bool` — true se há histórico |

**Nota**: NavigateTo agora limpa o histórico de avanço (_forwardHistory) antes de empurrar o estado atual.

#### `IDialogService.cs`
**Responsabilidade**: Gestão de dialogs modais.

| Método | Retorno | Descrição |
|--------|---------|-----------|
| `ShowDialogAsync<TViewModel>()` | `Task<bool>` | Abre dialog, retorna confirmado/cancelado |
| `ShowDialogAsync<TViewModel, TResult>()` | `Task<TResult?>` | Dialog com resultado tipado |
| `ConfirmAsync(title, message)` | `Task<bool>` | Dialog de confirmação simples (usa DialogWindow) |
| `AlertAsync(title, message)` | `Task` | Dialog de alerta simples (usa DialogWindow) |

### IO

#### `IAppPaths.cs`
**Responsabilidade**: Caminhos absolutos do sistema de ficheiros da aplicação.

| Propriedade | Descrição |
|-------------|-----------|
| AppDataRoot | LocalApplicationData/DevTracker/ |
| DatabasePath | data.db |
| LogsPath | logs/ |
| ConfigPath | config.json |
| WorkspaceRoot | Pasta raiz do workspace escolhida pelo utilizador |
| IsWorkspaceConfigured | False antes da primeira configuração (FirstRunSetup) |

**Observação**: Construído uma vez no bootstrap. `IsWorkspaceConfigured` é false antes da primeira configuração.

#### `IWorkspaceService.cs`
**Responsabilidade**: Ponto único de acesso ao filesystem do workspace.

| Método | Descrição |
|--------|-----------|
| `EnsureWorkspaceRootAsync(path)` | Cria pasta workspace com subpasta _archive |
| `CreateProjectFolderAsync(projectId, name)` | Cria pasta de projeto no workspace |
| `RenameProjectFolderAsync(projectId, currentPath, newName)` | Renomeia pasta de projeto |
| `ArchiveProjectFolderAsync(projectId, currentPath)` | Move para _archive |
| `CreateRepositoryFolderAsync(projectId, projectPath, repoName)` | Cria subpasta de repositório |
| `RenameRepositoryFolderAsync(repoId, repoPath, newName)` | Renomeia pasta de repositório |
| `ArchiveRepositoryFolderAsync(repoId, repoPath)` | Move para _archive |
| `AttachExistingFolderAsync(projectId, sourcePath)` | Move/copia pasta externa para workspace |
| `ListEntriesAsync(absolutePath)` | **Lista ficheiros e pastas (read-only). Retorna empty se inválido.** |
| `CopyDirectoryContentsAsync(source, dest)` | **Copia conteúdo recursivamente (cross-device safe).** |
| `IsInsideWorkspace(absolutePath)` | Verifica se path está dentro do WorkspaceRoot |
| `NormalizeAbsolutePath(path)` | Normaliza path para comparação |

**Regra**: Nenhum outro serviço, ViewModel ou helper deve usar `Directory.*` ou `File.*` para lógica de negócio.

**Novo record**:
```csharp
public sealed record WorkspaceEntry(string Name, string FullPath, bool IsDirectory);
```

### Settings

#### `ISettingsService.cs`
**Responsabilidade**: Configuração persistida em config.json (não na BD SQLite).

| Método | Retorno | Descrição |
|--------|---------|-----------|
| `LoadAsync()` | `Task<AppSettings>` | Retorna defaults se não existir ou corrompido |
| `SaveAsync(settings)` | `Task<Result>` | Persiste em config.json |
| `IsInitializedAsync()` | `Task<bool>` | True se WorkspaceRoot existe no disco |
| `Current` | `AppSettings` | Acesso síncrono às settings carregadas em memória |

**Regra**: Nunca guardar segredos, passwords ou tokens nas settings.

### Serviços de Domínio

#### `IProjectService.cs`
| Método | Retorno | Descrição |
|--------|---------|-----------|
| `GetAllAsync()` | `Result<IReadOnlyList<ProjectSummaryDto>>` | Lista todos os projetos |
| `GetByIdAsync(projectId)` | `Result<ProjectDetailDto>` | Detalhe de um projeto |
| `CreateAsync(request)` | `Result<Guid>` | Cria projeto |
| `UpdateAsync(request)` | `Result` | Atualiza projeto |
| `ChangeStateAsync(request)` | `Result` | Altera estado e regista transição |
| `ArchiveAsync(projectId)` | `Result` | Soft delete |
| `DeleteAsync(projectId)` | `Result` | Delete físico |

#### `IWorkItemService.cs`
| Método | Retorno | Descrição |
|--------|---------|-----------|
| `GetByProjectAsync(projectId, filterStatus?, filterType?)` | `Result<IReadOnlyList<WorkItemSummaryDto>>` | Lista com filtros opcionais |
| `GetByIdAsync(workItemId)` | `Result<WorkItemDetailDto>` | Detalhe de um work item |
| `CreateAsync(request)` | `Result<Guid>` | Cria work item |
| `UpdateAsync(request)` | `Result` | Atualiza work item |
| `ChangeStatusAsync(request)` | `Result` | Altera status |
| `AddCommentAsync(request)` | `Result` | Adiciona comentário |
| `DeleteAsync(workItemId)` | `Result` | Soft delete |

#### `IUserService.cs`
| Método | Retorno | Descrição |
|--------|---------|-----------|
| `GetAllAsync()` | `Result<IReadOnlyList<UserSummaryDto>>` | Lista utilizadores |
| `GetByIdAsync(userId)` | `Result<UserSummaryDto>` | Detalhe de utilizador |
| `CreateAsync(request)` | `Result<Guid>` | Cria utilizador |
| `ChangeRoleAsync(request)` | `Result` | Altera role |
| `DeactivateAsync(userId)` | `Result` | Desativa utilizador |
| `DeleteAsync(userId)` | `Result` | Apaga utilizador |

#### `IAuditService.cs`
**Regra**: Nunca expor Update ou Delete — AuditEntry é imutável por design.

| Método | Retorno | Descrição |
|--------|---------|-----------|
| `LogAsync(...)` | `Task` | INSERT de entrada de audit |
| `GetRecentAsync(count)` | `Result<IReadOnlyList<AuditEntryDto>>` | Últimas N entradas |
| `GetByEntityAsync(entityType, entityId)` | `Result<IReadOnlyList<AuditEntryDto>>` | Entradas por entidade |
| `GetByUserAsync(userId)` | `Result<IReadOnlyList<AuditEntryDto>>` | Entradas por utilizador |

#### `IRepositoryService.cs`
| Método | Retorno | Descrição |
|--------|---------|-----------|
| `GetByProjectAsync(projectId)` | `Result<IReadOnlyList<RepositorySummaryDto>>` | Lista repositórios |
| `CreateAsync(request)` | `Result<Guid>` | Cria repositório |
| `AttachAsync(request)` | `Result<Guid>` | Anexa pasta existente |
| `RenameAsync(repoId, newName)` | `Result` | Renomeia repositório |
| `DeleteAsync(repoId)` | `Result` | Apaga repositório |

#### `IDashboardService.cs`
| Método | Retorno | Descrição |
|--------|---------|-----------|
| `GetUserMetricsAsync()` | `Result<DashboardMetricsDto>` | Métricas da dashboard |

---

## Common

### `Result.cs`
**Responsabilidade**: Padrão Result<T> para evitar exceções em erros esperados.

```csharp
public sealed record Result(bool Succeeded, string? Error = null);
public sealed record Result<T>(bool Succeeded, T? Value = default, string? Error = null);
```

**Regra**: Serviços retornam sempre `Result`/`Result<T>` — nunca lançam exceções para erros esperados. A única exceção permitida é `AuthorizationException`.

### `AuthorizationException.cs`
**Responsabilidade**: Única exceção permitida para falhas de autorização.

```csharp
public sealed class AuthorizationException(string message) : Exception(message);
```

**Lançada por**: `IPermissionService.EnsureCanPerformAsync`
**Capturada por**: ViewModels para apresentar mensagem de acesso negado.

---

## Security

### `Permissions.cs`
**Responsabilidade**: Constantes de operações protegidas.

**Categorias**:
```
project.create/read/update/rename/change-state/delete/archive
repository.create/read/rename/delete/attach
workitem.create/read/update/delete/assign/change-status/comment
user.read/create/update/delete/change-role
security.change-password/re-authenticate
audit.read
settings.read/update
```

**Regra**: Usar sempre estas constantes — nunca strings literais.

---

## Settings

### `AppSettings.cs`
**Responsabilidade**: Modelo de configuração persistido em config.json.

```csharp
public sealed class AppSettings
{
    public string        WorkspaceRoot             { get; set; } = string.Empty;
    public int           SessionIdleTimeoutMinutes { get; set; } = 15;
    public int           SessionMaxDurationHours   { get; set; } = 8;
    public int           SchemaVersion             { get; set; } = 1;
    public string        GitHubUsername            { get; set; } = string.Empty;  // Persistido de forma segura
    public UiPreferences Ui                        { get; set; } = new();
}

public sealed class UiPreferences
{
    public string Theme        { get; set; } = "System";  // Light, Dark, System
    public string Language     { get; set; } = "pt-PT";
    public double SidebarWidth { get; set; } = 240;
}
```

**Regra**: Nunca guardar segredos aqui. `GitHubUsername` é não-sensível; `GitHubToken` é **apenas em memória**.

---

## DTOs

### `ProjectDtos.cs`

**Requests**:
```csharp
public sealed record CreateProjectRequest(
    string Name, string? Description, string Color, string WorkspacePath, string? GitHubRepositoryUrl);

public sealed record UpdateProjectRequest(
    Guid Id, string Name, string? Description, string Color, string? GitHubRepositoryUrl);

public sealed record ChangeProjectStateRequest(
    Guid ProjectId, ProjectState NewState, string? Reason);
```

**Responses**:
```csharp
public sealed record ProjectSummaryDto(
    Guid Id, string Name, string? Description, string Color, ProjectState State,
    int WorkItemCount, int RepositoryCount, string? GitHubRepositoryUrl,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record ProjectDetailDto(
    Guid Id, string Name, string? Description, string Color, string WorkspacePath,
    ProjectState State, string? GitHubRepositoryUrl,
    IReadOnlyList<RepositorySummaryDto> Repositories,
    IReadOnlyList<WorkItemSummaryDto> WorkItems,
    DateTime CreatedAt, DateTime UpdatedAt);
```

### `WorkItemDtos.cs`

**Requests**:
```csharp
public sealed record CreateWorkItemRequest(
    Guid ProjectId, string Title, string? Description,
    WorkItemType Type, WorkItemPriority Priority, WorkItemDifficulty Difficulty,
    WorkItemEstimatedTime EstimatedTime, Guid? AssignedToUserId, DateTime? DueDate);

public sealed record UpdateWorkItemRequest(
    Guid Id, string Title, string? Description,
    WorkItemType Type, WorkItemPriority Priority, WorkItemDifficulty Difficulty,
    WorkItemEstimatedTime EstimatedTime, Guid? AssignedToUserId, DateTime? DueDate);

public sealed record ChangeWorkItemStatusRequest(Guid WorkItemId, WorkItemStatus NewStatus);
public sealed record AddCommentRequest(Guid WorkItemId, string Body);
```

**Responses**:
```csharp
public sealed record WorkItemSummaryDto(
    Guid Id, int Number, string Title, WorkItemType Type, WorkItemStatus Status,
    WorkItemPriority Priority, WorkItemDifficulty Difficulty, WorkItemEstimatedTime EstimatedTime,
    string? AssignedToUsername, DateTime? DueDate, DateTime UpdatedAt);

public sealed record WorkItemDetailDto(
    Guid Id, int Number, string Title, string? Description,
    WorkItemType Type, WorkItemStatus Status, WorkItemPriority Priority,
    WorkItemDifficulty Difficulty, WorkItemEstimatedTime EstimatedTime,
    Guid ProjectId, string ProjectName, string CreatedByUsername,
    string? AssignedToUsername, IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<string> Labels, DateTime? DueDate,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CommentDto(
    Guid Id, string AuthorUsername, string Body, DateTime CreatedAt, DateTime? EditedAt);
```

### `UserDtos.cs`

**Requests**:
```csharp
public sealed record CreateUserRequest(
    string Username, string Password, string ConfirmPassword, UserRole Role);

public sealed record ChangeUserRoleRequest(Guid TargetUserId, UserRole NewRole);
```

**Response**:
```csharp
public sealed record UserSummaryDto(
    Guid Id, string Username, UserRole Role, bool IsActive,
    DateTime? LastLoginAt, DateTime CreatedAt);
```

### `RepositoryDtos.cs`

**Requests**:
```csharp
public sealed record CreateRepositoryRequest(Guid ProjectId, string Name, string? Description);
public sealed record AttachRepositoryRequest(Guid ProjectId, string SourceAbsolutePath, string? Description);
```

**Response**:
```csharp
public sealed record RepositorySummaryDto(
    Guid Id, string Name, string RelativePath, string? Description, DateTime CreatedAt);
```

### `AuditEntryDto.cs`
```csharp
public sealed record AuditEntryDto(
    Guid Id, string? Username, AuditAction Action, string EntityType,
    Guid? EntityId, string? OldValue, string? NewValue,
    DateTime Timestamp, string? MachineName);
```

### `DashboardDtos.cs`
```csharp
public sealed record DashboardMetricsDto(
    int TotalProjects, int ActiveProjects, int CompletedTasks,
    int PendingTasks, int InProgressTasks, double EstimatedHoursRemaining,
    TaskBreakdownDto TaskBreakdown);

public sealed record TaskBreakdownDto(
    DifficultyBreakdownDto Difficulty,
    PriorityBreakdownDto Priority,
    TypeBreakdownDto Type);

public sealed record DifficultyBreakdownDto(int VeryEasy, int Easy, int Medium, int Hard, int VeryHard);
public sealed record PriorityBreakdownDto(int Low, int Medium, int High, int Critical);
public sealed record TypeBreakdownDto(int Task, int Feature, int Bug, int Improvement, int Documentation);
```

---

## Validators (FluentValidation)

### `CreateProjectRequestValidator.cs`
```csharp
RuleFor(x => x.Name)
    .NotEmpty()
    .MinimumLength(2).WithMessage("O nome deve ter pelo menos 2 caracteres.")
    .MaximumLength(128).WithMessage("O nome não pode ter mais de 128 caracteres.")
    .Matches(@"^[^/\:*?""<>|]+$").WithMessage("O nome contém caracteres inválidos.");

RuleFor(x => x.Color)
    .NotEmpty()
    .Matches(@"^#[0-9A-Fa-f]{6}$").WithMessage("A cor deve ser um código hex válido.");

RuleFor(x => x.Description)
    .MaximumLength(1024).When(x => x.Description is not null);
```

### `ChangeProjectStateRequestValidator.cs`
```csharp
// Reason obrigatório para Paused e Cancelled
private static readonly HashSet<ProjectState> StatesRequiringReason = 
    [ProjectState.Paused, ProjectState.Cancelled];

RuleFor(x => x.Reason)
    .NotEmpty().WithMessage("É obrigatório indicar a razão para pausar ou cancelar o projeto.")
    .When(x => StatesRequiringReason.Contains(x.NewState));
```

### `WorkItemRequestValidators.cs`
**CreateWorkItemRequestValidator**:
```csharp
RuleFor(x => x.ProjectId).NotEmpty();
RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
RuleFor(x => x.Description).MaximumLength(4096).When(x => x.Description is not null);
RuleFor(x => x.DueDate).GreaterThan(DateTime.UtcNow).When(x => x.DueDate.HasValue);
```

**AddCommentRequestValidator**:
```csharp
RuleFor(x => x.WorkItemId).NotEmpty();
RuleFor(x => x.Body).NotEmpty().MaximumLength(4096);
```

### `CreateUserRequestValidator.cs`
```csharp
RuleFor(x => x.Username)
    .NotEmpty().MinimumLength(3).MaximumLength(64)
    .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("O username só pode conter letras, números, '.', '_' e '-'.");

RuleFor(x => x.Password)
    .NotEmpty().MinimumLength(12)
    .Matches(@"[A-Z]").Matches(@"[a-z]").Matches(@"\d").Matches(@"[^a-zA-Z0-9]");

RuleFor(x => x.ConfirmPassword).Equal(x => x.Password);
RuleFor(x => x.Role).NotEqual(UserRole.Owner);  // Só via bootstrap
```

### `AppSettingsValidator.cs`
```csharp
RuleFor(x => x.WorkspaceRoot).Must(Directory.Exists).When(x => !string.IsNullOrWhiteSpace(x.WorkspaceRoot));
RuleFor(x => x.SessionIdleTimeoutMinutes).InclusiveBetween(1, 480);
RuleFor(x => x.SessionMaxDurationHours).InclusiveBetween(1, 24);
RuleFor(x => x.Ui.Theme).Must(t => t is "Light" or "Dark" or "System");
```

---

## Padrões Aplicados

- **Result<T>**: Evita exceções em erros esperados
- **FluentValidation**: Validação declarativa antes de persistência
- **Interface Segregation**: Interfaces pequenas e focadas (IAuthService, ISessionService, etc.)
- **Imutabilidade**: AuditEntry e StateTransition só INSERT
- **Soft Delete**: Project, WorkItem, Repository, Comment usam IsDeleted + DeletedAt
