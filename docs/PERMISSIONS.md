# DevTracker — Authorization & Permissions Context

> Ficheiro de contexto: autorização central, operações protegidas, roles e política de permissões.
> Usar em conjunto com STACK.md, DOMAIN.md, AUTH.md e WORKSPACE.md.

---

## 1. Objetivo

O DevTracker precisa de uma camada de autorização **centralizada**, previsível e fácil de auditar.  
A UI pode esconder botões por UX, mas **a decisão real de acesso tem sempre de acontecer na camada de serviços**.

Este ficheiro define:
- operações protegidas
- mapeamento entre roles e operações
- contrato de `IPermissionService`
- implementação base de autorização por política simples
- como os serviços devem consumir permissões

---

## 2. Princípio base

**Nunca fazer autorização espalhada em `if (role == ...)` por todo o código.**

Em vez disso:
- definir nomes estáveis para operações
- validar via `IPermissionService`
- deixar a política central num único local

---

## 3. Roles

As roles do sistema são as já definidas em `UserRole`:

```csharp
public enum UserRole
{
    Owner = 0,
    Admin = 1,
    Maintainer = 2,
    Reader = 3
}
```

### Semântica das roles

- **Owner**: controlo total do sistema local, incluindo utilizadores, segurança e destruição crítica
- **Admin**: gestão operacional de projetos e trabalho, mas sem controlo total sobre owners
- **Maintainer**: manutenção diária de projetos, tarefas e repositórios sem operações administrativas sensíveis
- **Reader**: acesso apenas de leitura e comentário básico onde permitido

---

## 4. Operações protegidas

Todas as permissões são modeladas como strings estáveis, com naming consistente:

```csharp
// src/DevTracker.Application/Security/Permissions.cs
namespace DevTracker.Application.Security;

public static class Permissions
{
    // Projects
    public const string ProjectCreate = "project.create";
    public const string ProjectRead = "project.read";
    public const string ProjectUpdate = "project.update";
    public const string ProjectRename = "project.rename";
    public const string ProjectChangeState = "project.change-state";
    public const string ProjectDelete = "project.delete";
    public const string ProjectArchive = "project.archive";

    // Repositories
    public const string RepositoryCreate = "repository.create";
    public const string RepositoryRead = "repository.read";
    public const string RepositoryRename = "repository.rename";
    public const string RepositoryDelete = "repository.delete";
    public const string RepositoryAttach = "repository.attach";

    // Work items
    public const string WorkItemCreate = "workitem.create";
    public const string WorkItemRead = "workitem.read";
    public const string WorkItemUpdate = "workitem.update";
    public const string WorkItemDelete = "workitem.delete";
    public const string WorkItemAssign = "workitem.assign";
    public const string WorkItemChangeStatus = "workitem.change-status";
    public const string WorkItemComment = "workitem.comment";

    // Users & security
    public const string UserRead = "user.read";
    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserDelete = "user.delete";
    public const string UserChangeRole = "user.change-role";
    public const string SecurityChangePassword = "security.change-password";
    public const string SecurityReAuthenticate = "security.re-authenticate";

    // Audit & settings
    public const string AuditRead = "audit.read";
    public const string SettingsRead = "settings.read";
    public const string SettingsUpdate = "settings.update";
}
```

---

## 5. Matriz de permissões

| Operação | Owner | Admin | Maintainer | Reader |
|---|---|---|---|---|
| `project.create` | ✅ | ✅ | ❌ | ❌ |
| `project.read` | ✅ | ✅ | ✅ | ✅ |
| `project.update` | ✅ | ✅ | ✅ | ❌ |
| `project.rename` | ✅ | ✅ | ❌ | ❌ |
| `project.change-state` | ✅ | ✅ | ✅ | ❌ |
| `project.delete` | ✅ | ❌ | ❌ | ❌ |
| `project.archive` | ✅ | ✅ | ❌ | ❌ |
| `repository.create` | ✅ | ✅ | ✅ | ❌ |
| `repository.read` | ✅ | ✅ | ✅ | ✅ |
| `repository.rename` | ✅ | ✅ | ✅ | ❌ |
| `repository.delete` | ✅ | ✅ | ❌ | ❌ |
| `repository.attach` | ✅ | ✅ | ✅ | ❌ |
| `workitem.create` | ✅ | ✅ | ✅ | ❌ |
| `workitem.read` | ✅ | ✅ | ✅ | ✅ |
| `workitem.update` | ✅ | ✅ | ✅ | ❌ |
| `workitem.delete` | ✅ | ✅ | ❌ | ❌ |
| `workitem.assign` | ✅ | ✅ | ✅ | ❌ |
| `workitem.change-status` | ✅ | ✅ | ✅ | ❌ |
| `workitem.comment` | ✅ | ✅ | ✅ | ✅ |
| `user.read` | ✅ | ✅ | ✅* | ❌ |
| `user.create` | ✅ | ❌ | ❌ | ❌ |
| `user.update` | ✅ | ❌ | ❌ | ❌ |
| `user.delete` | ✅ | ❌ | ❌ | ❌ |
| `user.change-role` | ✅ | ❌ | ❌ | ❌ |
| `security.change-password` | ✅ | ✅ | ✅ | ✅ |
| `security.re-authenticate` | ✅ | ✅ | ✅ | ✅ |
| `audit.read` | ✅ | ✅ | ❌ | ❌ |
| `settings.read` | ✅ | ✅ | ✅ | ✅ |
| `settings.update` | ✅ | ✅ | ❌ | ❌ |

> **\* `user.read` para Maintainer:** O Maintainer tem acesso de leitura limitado a utilizadores para poder ver quem está atribuído a um work item, ver perfis básicos, e pesquisar utilizadores para atribuição. **Não inclui** acesso a dados sensíveis (password hash, salt, audit pessoal de outros). Esta permissão aplica-se a listagens e lookups de username/role, não a gestão de utilizadores (criar, editar, apagar continuam exclusivos do Owner).

---

## 6. Interface de autorização

```csharp
// src/DevTracker.Application/Abstractions/Security/IPermissionService.cs
namespace DevTracker.Application.Abstractions.Security;

public interface IPermissionService
{
    Task<bool> CanPerformAsync(Guid userId, string permission, CancellationToken ct = default);
    Task EnsureCanPerformAsync(Guid userId, string permission, CancellationToken ct = default);
}
```

### Regra

- `CanPerformAsync(...)` para fluxos normais e UI adaptativa
- `EnsureCanPerformAsync(...)` para falhar rápido com exceção de autorização na camada de aplicação

---

## 7. Exceção de autorização

```csharp
// src/DevTracker.Application/Common/Exceptions/AuthorizationException.cs
namespace DevTracker.Application.Common.Exceptions;

public sealed class AuthorizationException(string message) : Exception(message);
```

---

## 8. Implementação base do PermissionService

```csharp
// src/DevTracker.Infrastructure/Security/PermissionService.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Common.Exceptions;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Security;

public sealed class PermissionService(AppDbContext db) : IPermissionService
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Matrix =
        new Dictionary<UserRole, HashSet<string>>
        {
            [UserRole.Owner] =
            [
                Permissions.ProjectCreate,
                Permissions.ProjectRead,
                Permissions.ProjectUpdate,
                Permissions.ProjectRename,
                Permissions.ProjectChangeState,
                Permissions.ProjectDelete,
                Permissions.ProjectArchive,

                Permissions.RepositoryCreate,
                Permissions.RepositoryRead,
                Permissions.RepositoryRename,
                Permissions.RepositoryDelete,
                Permissions.RepositoryAttach,

                Permissions.WorkItemCreate,
                Permissions.WorkItemRead,
                Permissions.WorkItemUpdate,
                Permissions.WorkItemDelete,
                Permissions.WorkItemAssign,
                Permissions.WorkItemChangeStatus,
                Permissions.WorkItemComment,

                Permissions.UserRead,
                Permissions.UserCreate,
                Permissions.UserUpdate,
                Permissions.UserDelete,
                Permissions.UserChangeRole,

                Permissions.SecurityChangePassword,
                Permissions.SecurityReAuthenticate,

                Permissions.AuditRead,
                Permissions.SettingsRead,
                Permissions.SettingsUpdate
            ],
            [UserRole.Admin] =
            [
                Permissions.ProjectCreate,
                Permissions.ProjectRead,
                Permissions.ProjectUpdate,
                Permissions.ProjectRename,
                Permissions.ProjectChangeState,
                Permissions.ProjectArchive,

                Permissions.RepositoryCreate,
                Permissions.RepositoryRead,
                Permissions.RepositoryRename,
                Permissions.RepositoryDelete,
                Permissions.RepositoryAttach,

                Permissions.WorkItemCreate,
                Permissions.WorkItemRead,
                Permissions.WorkItemUpdate,
                Permissions.WorkItemDelete,
                Permissions.WorkItemAssign,
                Permissions.WorkItemChangeStatus,
                Permissions.WorkItemComment,

                Permissions.UserRead,
                Permissions.SecurityChangePassword,
                Permissions.SecurityReAuthenticate,
                Permissions.AuditRead,
                Permissions.SettingsRead,
                Permissions.SettingsUpdate
            ],
            [UserRole.Maintainer] =
            [
                Permissions.ProjectRead,
                Permissions.ProjectUpdate,
                Permissions.ProjectChangeState,

                Permissions.RepositoryCreate,
                Permissions.RepositoryRead,
                Permissions.RepositoryRename,
                Permissions.RepositoryAttach,

                Permissions.WorkItemCreate,
                Permissions.WorkItemRead,
                Permissions.WorkItemUpdate,
                Permissions.WorkItemAssign,
                Permissions.WorkItemChangeStatus,
                Permissions.WorkItemComment,

                Permissions.UserRead, // leitura limitada: lookup de username/role para atribuicão

                Permissions.SecurityChangePassword,
                Permissions.SecurityReAuthenticate,
                Permissions.SettingsRead
            ],
            [UserRole.Reader] =
            [
                Permissions.ProjectRead,
                Permissions.RepositoryRead,
                Permissions.WorkItemRead,
                Permissions.WorkItemComment,
                Permissions.SecurityChangePassword,
                Permissions.SecurityReAuthenticate,
                Permissions.SettingsRead
            ]
        };

    public async Task<bool> CanPerformAsync(Guid userId, string permission, CancellationToken ct = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);

        if (user is null)
            return false;

        return Matrix.TryGetValue(user.Role, out var permissions)
               && permissions.Contains(permission);
    }

    public async Task EnsureCanPerformAsync(Guid userId, string permission, CancellationToken ct = default)
    {
        var can = await CanPerformAsync(userId, permission, ct);
        if (!can)
            throw new AuthorizationException($"O utilizador não tem permissão '{permission}'.");
    }
}
```

---

## 9. Como usar nos serviços

### Exemplo: ProjectService

```csharp
public sealed class ProjectService(
    IPermissionService permissionService,
    ICurrentUserService currentUser,
    IWorkspaceService workspaceService,
    AppDbContext db) : IProjectService
{
    public async Task<Result<Guid>> CreateProjectAsync(string name, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result<Guid>.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(
            currentUser.UserId.Value,
            Permissions.ProjectCreate,
            ct);

        // resto da lógica...
    }
}
```

### Exemplo: WorkItemService

```csharp
await permissionService.EnsureCanPerformAsync(
    currentUser.UserId!.Value,
    Permissions.WorkItemChangeStatus,
    ct);
```

---

## 10. Regras adicionais por contexto

A matriz por role resolve a maior parte dos casos, mas alguns cenários exigem regras contextuais.

### Exemplo 1: Owner não pode apagar o último Owner

Mesmo que `Owner` tenha `user.delete`, deve existir uma regra adicional:

```csharp
if (targetUser.Role == UserRole.Owner)
{
    var ownersCount = await db.Users.CountAsync(x => x.Role == UserRole.Owner && x.IsActive, ct);
    if (ownersCount <= 1)
        return Result.Fail("Não é possível remover o último Owner do sistema.");
}
```

### Exemplo 2: Admin não pode promover alguém a Owner

Mesmo que no futuro `Admin` ganhe mais poderes, esta regra deve continuar explícita.

### Exemplo 3: utilizador só pode mudar a própria password

`security.change-password` existe para todos, mas a regra contextual é:
- o próprio utilizador pode mudar a sua password
- mudar password de outro user é operação administrativa separada e, idealmente, nem deve existir

---

## 11. UI adaptativa

A UI pode consultar permissões para esconder ações não permitidas.

### Exemplo

```csharp
CanDeleteProject = await permissionService.CanPerformAsync(
    currentUser.UserId!.Value,
    Permissions.ProjectDelete,
    ct);
```

### Regra crítica

Isto é **apenas UX**.  
Mesmo que a UI esconda o botão, o serviço continua a validar.  
Nunca confiar na UI como barreira de segurança.

---

## 12. IPermissionSnapshotService — obrigatório nos ViewModels

Os ViewModels **não devem** chamar `IPermissionService.CanPerformAsync` individualmente para cada botão/ação — isso gera N queries à BD por ecrã. Em vez disso, carregar o snapshot uma vez no `OnActivatedAsync` e usar o `HashSet<string>` local.

```csharp
// src/DevTracker.Application/Abstractions/Security/IPermissionSnapshotService.cs
namespace DevTracker.Application.Abstractions.Security;

public interface IPermissionSnapshotService
{
    /// <summary>
    /// Devolve o conjunto completo de permissões do utilizador autenticado atual.
    /// Usar nos ViewModels para popular propriedades bool de UI (CanCreate, CanDelete, etc.).
    /// Não substitui IPermissionService na camada de serviços.
    /// </summary>
    Task<HashSet<string>> GetCurrentUserPermissionsAsync(CancellationToken ct = default);
}
```

### Implementação

```csharp
// src/DevTracker.Infrastructure/Security/PermissionSnapshotService.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Security;
using DevTracker.Core.Enums;

namespace DevTracker.Infrastructure.Security;

public sealed class PermissionSnapshotService(
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IPermissionSnapshotService
{
    // Cache simples em memória por sessão — inválido após logout
    private HashSet<string>? _cached;
    private Guid? _cachedForUserId;

    public async Task<HashSet<string>> GetCurrentUserPermissionsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;

        // Cache miss ou utilizador diferente
        if (_cached is null || _cachedForUserId != userId)
        {
            if (userId is null)
            {
                _cached = [];
                _cachedForUserId = null;
                return _cached;
            }

            // Obter todas as permissões da matriz para a role do utilizador
            // Reutiliza a mesma matriz do PermissionService
            _cached = await ResolvePermissionsAsync(userId.Value, ct);
            _cachedForUserId = userId;
        }

        return _cached;
    }

    public void InvalidateCache()
    {
        _cached = null;
        _cachedForUserId = null;
    }

    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Matrix =
        PermissionService.GetMatrix(); // expor a matriz como método estático interno

    private Task<HashSet<string>> ResolvePermissionsAsync(Guid userId, CancellationToken ct)
    {
        // PermissionService já faz a query à BD;
        // aqui fazemos uma única query e devolvemos o HashSet completo
        // Alternativa mais simples: expor GetRoleForUserAsync no repositório
        return Task.FromResult(
            Matrix.TryGetValue(currentUser.Role ?? UserRole.Reader, out var perms)
                ? new HashSet<string>(perms)
                : []);
    }
}
```

> **Nota de implementação:** `PermissionSnapshotService` é `Scoped` (vive por operação).
> O cache interno é por instância, não global — não há risco de dados de um utilizador serem vistos por outro.
> O cache é automáticamente descartado no próximo Scope (ex: próxima navegação).
> Para invalidar explicitamente após mudança de role, chamar `InvalidateCache()`.

### Uso no ViewModel (padrão obrigatório)

```csharp
public override async Task OnActivatedAsync(CancellationToken ct = default)
{
    // UMA única chamada — devolve HashSet em memória
    var permissions = await _permissionSnapshot.GetCurrentUserPermissionsAsync(ct);

    CanCreateProject  = permissions.Contains(Permissions.ProjectCreate);
    CanDeleteProject  = permissions.Contains(Permissions.ProjectDelete);
    CanArchiveProject = permissions.Contains(Permissions.ProjectArchive);
    CanManageUsers    = permissions.Contains(Permissions.UserCreate);
    // ...
}
```

Esto não substitui `IPermissionService` — a UI apenas adapta a apresentação. A verificação real contínua a acontecer nos serviços.

### Registo no DI

```csharp
services.AddScoped<IPermissionSnapshotService, PermissionSnapshotService>();
```

---

## 13. Audit e autorização

Sempre que uma operação falhar por autorização, existem duas abordagens possíveis:

### Abordagem A — não auditar falhas de permissão
Mais simples, menos ruído.

### Abordagem B — auditar tentativas bloqueadas sensíveis
Mais útil para segurança.

### Recomendação

Auditar apenas falhas sensíveis, por exemplo:
- tentativa de apagar projeto sem permissão
- tentativa de gerir utilizadores sem permissão
- tentativa de ver audit log sem permissão

Isto pode ser feito nos serviços mais críticos, não precisa estar dentro do `PermissionService`.

---

## 14. Dependency Injection

```csharp
// src/DevTracker.Infrastructure/DependencyInjection.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Infrastructure;

public static partial class DependencyInjection
{
    public static IServiceCollection AddAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        return services;
    }
}
```

---

## 15. Melhorias futuras

### Possível evolução 1 — permissões configuráveis em BD

Hoje a matriz está hardcoded por simplicidade e robustez.  
No futuro, podes mover isto para tabelas:
- `RolePermission`
- `UserPermissionOverride`

### Possível evolução 2 — authorization handlers por recurso

Se o sistema crescer muito, podes passar de policy simples por string para algo mais rico:
- `CanDeleteProject(user, project)`
- `CanChangeRole(actor, targetUser)`
- `CanArchiveRepository(user, repository)`

### Possível evolução 3 — claims em memória na sessão

Quando fizer sentido otimizar, a sessão pode guardar as permissões já resolvidas.  
Mas por agora é melhor manter simples e correto.

---

## 16. Regras finais

1. Toda a autorização passa por `IPermissionService`.
2. A UI só esconde ações; não decide segurança.
3. Roles definem permissões base.
4. Regras contextuais vivem nos serviços específicos.
5. Operações sensíveis podem ser auditadas quando bloqueadas.
6. Nunca espalhar `if (role == ...)` pelo código todo.

