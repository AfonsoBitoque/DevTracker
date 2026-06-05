# DevTracker.Core - Análise Técnica

Camada de domínio puro. Contém entidades, enums e value objects. Sem dependências externas.

---

## Enums

### `WorkItemEnums.cs`
**Responsabilidade**: Define os enums de domínio para work items.

| Enum | Valores |
|------|---------|
| `WorkItemType` | Task(0), Feature(1), Bug(2), Improvement(3), Documentation(4) |
| `WorkItemStatus` | Backlog(0), Todo(1), InProgress(2), InReview(3), Done(4), Cancelled(5) |
| `WorkItemPriority` | Low(0), Medium(1), High(2), Critical(3) |
| `WorkItemDifficulty` | VeryEasy(0), Easy(1), Medium(2), Hard(3), VeryHard(4) |
| `WorkItemEstimatedTime` | LessThan1Hour(0), OneToTwoHours(1), HalfDay(2), OneDay(3), TwoToThreeDays(4), MoreThanThreeDays(5) |

**Observação**: Transições de status são livres — não há máquina de estados rígida neste nível.

### `UserRole.cs`
**Responsabilidade**: Define roles RBAC com descrições de nível de acesso.

| Valor | Descrição |
|-------|-----------|
| Owner(0) | Controlo total, incluindo gestão de utilizadores |
| Admin(1) | Gestão operacional, sem controlo administrativo total |
| Maintainer(2) | Manutenção diária de projetos e tarefas |
| Reader(3) | Acesso apenas de leitura e comentário |

### `ProjectState.cs`
**Responsabilidade**: Estados de um projeto.

| Valor | Descrição |
|-------|-----------|
| NotStarted(0) | Projeto ainda não iniciado |
| InProgress(1) | Em desenvolvimento |
| Paused(2) | Pausado — requer Reason |
| Cancelled(3) | Cancelado — requer Reason |
| Completed(4) | Concluído |

**Regra**: Paused e Cancelled requerem Reason (validado por FluentValidation em Application).

### `AuditAction.cs`
**Responsabilidade**: Categorias de ações auditáveis com intervalos por domínio.

| Intervalo | Domínio |
|-----------|---------|
| 100–199 | Auth (Login, Logout, PasswordChanged) |
| 200–299 | Project (Created, Updated, StateChanged, Deleted) |
| 300–399 | Repository (Created, Renamed, Deleted) |
| 400–499 | WorkItem (Created, Updated, StatusChanged, Deleted) |
| 500–599 | User (Created, Updated, Deactivated) |

---

## Entities

### `BaseEntity.cs`
**Responsabilidade**: Classe abstrata base para todas as entidades com identidade própria.

```csharp
public abstract class BaseEntity
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

**Padrão**: `UpdatedAt` é atualizado automaticamente por `AppDbContext.SaveChanges()` via override de `UpdateTimestamps()`. O valor default no campo é apenas o valor de criação C# — a atualização automática só acontece no override.

### `User.cs`
**Responsabilidade**: Utilizador local do sistema. Autenticação via Argon2id (PasswordHash + Salt).

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| Username | string | Único, 3-64 chars |
| PasswordHash | string | Hash Argon2id em Base64 |
| Salt | string | Salt aleatório 32 bytes em Base64 |
| Role | UserRole | Default Reader |
| IsActive | bool | Default true |
| LastLoginAt | DateTime? | Último login |

**Navegação**:
- `CreatedWorkItems` → WorkItem (Restrict on delete)
- `AssignedWorkItems` → WorkItem (SetNull on delete)
- `Comments` → Comment

**Segurança**: A sessão é gerida em memória por `ISessionService` — nunca persistida.

### `Project.cs`
**Responsabilidade**: Projeto principal. Tem uma pasta física no WorkspaceRoot gerida por `IWorkspaceService`.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| Name | string | 2-128 chars |
| Description | string? | Opcional |
| Color | string | Hex #RRGGBB |
| WorkspacePath | string | Caminho absoluto no disco |
| GitHubRepositoryUrl | string? | URL para integração Git |
| State | ProjectState | Default NotStarted |
| IsDeleted | bool | Soft delete flag |
| DeletedAt | DateTime? | Soft delete timestamp |

**Navegação**: Repositories, WorkItems, StateTransitions

**Regra**: Soft delete (`IsDeleted=true + DeletedAt`). Para ver projetos apagados usar `.IgnoreQueryFilters()`.

### `WorkItem.cs`
**Responsabilidade**: Tarefa/issue de um projeto.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| ProjectId | Guid | FK para Project |
| Number | int | Sequencial único por projeto |
| Title | string | Max 256 chars |
| Description | string? | Opcional |
| Type | WorkItemType | Default Task |
| Status | WorkItemStatus | Default Backlog |
| Priority | WorkItemPriority | Default Medium |
| Difficulty | WorkItemDifficulty | Default Medium |
| EstimatedTime | WorkItemEstimatedTime | Default OneDay |
| CreatedByUserId | Guid | FK para User |
| AssignedToUserId | Guid? | FK opcional para User |
| DueDate | DateTime? | Data de entrega |
| IsDeleted | bool | Soft delete flag |
| DeletedAt | DateTime? | Soft delete timestamp |

**Navegação**: Project, CreatedByUser, AssignedToUser, Comments, Labels

**Regra crítica**: `Number` é gerado no serviço como `MAX(Number)+1` por projeto com `.IgnoreQueryFilters()` — contabiliza soft-deleted para evitar reutilização de números.

### `Comment.cs`
**Responsabilidade**: Comentário num work item.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| WorkItemId | Guid | FK para WorkItem |
| AuthorUserId | Guid | FK para User |
| Body | string | Conteúdo do comentário |
| EditedAt | DateTime? | Preenchido se editado |
| IsDeleted | bool | Soft delete flag |
| DeletedAt | DateTime? | Soft delete timestamp |

**Navegação**: WorkItem, AuthorUser

**Regra**: `EditedAt` fica preenchido quando o conteúdo é alterado após criação.

### `Label.cs`
**Responsabilidade**: Label de classificação reutilizável entre work items.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| Name | string | Max 64 chars |
| Color | string | Hex #RRGGBB, default #6366F1 |

**Navegação**: WorkItems (via junção WorkItemLabel)

### `WorkItemLabel.cs`
**Responsabilidade**: Tabela de junção entre WorkItem e Label.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| WorkItemId | Guid | Parte da chave composta |
| LabelId | Guid | Parte da chave composta |

**Regra**: Chave composta `(WorkItemId, LabelId)` configurada via EF Fluent API.

### `Repository.cs`
**Responsabilidade**: Repositório de código associado a um projeto.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| ProjectId | Guid | FK para Project |
| Name | string | Max 128 chars |
| RelativePath | string | Relativo ao WorkspacePath do projeto |
| Description | string? | Opcional |
| IsDeleted | bool | Soft delete flag |
| DeletedAt | DateTime? | Soft delete timestamp |

**Navegação**: Project

### `StateTransition.cs`
**Responsabilidade**: Registo imutável de uma transição de estado de projeto.

**NÃO herda BaseEntity** — não deve ser atualizado ou apagado.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| Id | Guid | PK |
| ProjectId | Guid | FK para Project |
| FromState | ProjectState | Estado anterior |
| ToState | ProjectState | Novo estado |
| Reason | string? | Razão (obrigatório para Paused/Cancelled) |
| ChangedByUserId | Guid | FK para User |
| ChangedAt | DateTime | Timestamp da transição |

**Navegação**: Project, ChangedByUser

**Regra**: `Reason` é obrigatório quando `ToState` é Paused ou Cancelled — validado por FluentValidation em Application, não no modelo.

### `AuditEntry.cs`
**Responsabilidade**: Registo imutável de auditoria.

**NÃO herda BaseEntity** — só INSERT, nunca UPDATE/DELETE.

| Propriedade | Tipo | Descrição |
|-------------|------|-----------|
| Id | Guid | PK |
| UserId | Guid? | Nullable — audit sobrevive ao delete do user |
| Action | AuditAction | Categoria da ação |
| EntityType | string | Nome do tipo de entidade |
| EntityId | Guid? | ID da entidade afetada |
| OldValue | string? | Valor anterior (JSON) |
| NewValue | string? | Novo valor (JSON) |
| Timestamp | DateTime | Default UtcNow |
| MachineName | string? | Default Environment.MachineName |

**Navegação**: User (nullable)

**Regras**:
- `OldValue/NewValue` devem conter JSON estruturado
- NUNCA guardar passwords, tokens ou dados sensíveis
- Se o utilizador for apagado, o audit fica com `UserId=null` (SetNull)
