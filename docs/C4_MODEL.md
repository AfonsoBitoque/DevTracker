# DevTracker — C4 Model

> Modelo de arquitectura C4 (Níveis 1, 2 e 3) do sistema DevTracker.
> Nível 4 (Code) não é documentado aqui — ver ficheiros de contexto individuais (DOMAIN.md, SERVICES.md, AUTH.md, etc.).

---

## Nível 1 — Context Diagram

> **Quem usa o sistema e com o que interage.**

```
╔══════════════════════════════════════════════════════════════════╗
║                         CONTEXT                                  ║
╚══════════════════════════════════════════════════════════════════╝

                         ┌──────────────────────┐
                         │  Utilizador           │
                         │  (Developer/Owner/    │
                         │   Admin/Maintainer/   │
                         │   Reader)             │
                         └──────────┬───────────┘
                                    │
                          Usa a app desktop
                          (Windows/Linux/macOS)
                                    │
                                    ▼
                    ┌───────────────────────────────┐
                    │                               │
                    │         DevTracker            │
                    │                               │
                    │  Aplicação desktop offline    │
                    │  para gestão pessoal de       │
                    │  projetos de software         │
                    │                               │
                    └───────────────┬───────────────┘
                                    │
               ┌────────────────────┼────────────────────┐
               │                    │                    │
               ▼                    ▼                    ▼
   ┌───────────────────┐  ┌──────────────────┐  ┌──────────────────┐
   │  Sistema de       │  │  SQLite          │  │  Sistema de      │
   │  Ficheiros Local  │  │  (data.db)       │  │  Configuração    │
   │                   │  │                  │  │  (config.json)   │
   │  Pasta workspace  │  │  Base de dados   │  │                  │
   │  com projetos e   │  │  local com toda  │  │  Preferências e  │
   │  repositórios     │  │  a informação    │  │  workspace root  │
   │  físicos no disco │  │  da aplicação    │  │                  │
   └───────────────────┘  └──────────────────┘  └──────────────────┘
```

### Notas do Nível 1

- **Zero dependências externas** — sem cloud, sem APIs remotas, sem autenticação de terceiros.
- O utilizador interage com a app desktop localmente.
- A app escreve em três locais no disco: base de dados SQLite, pasta workspace, ficheiro config.
- Todos os dados ficam no `LocalApplicationData` do SO ou no workspace definido pelo utilizador.

---

## Nível 2 — Container Diagram

> **Os processos/containers que compõem o sistema.**

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                              CONTAINERS                                      ║
╚══════════════════════════════════════════════════════════════════════════════╝

  ┌─────────────────────────────────────────────────────────────────────────┐
  │  DevTracker System                                                      │
  │                                                                         │
  │   ┌─────────────────────────────────────────────────────────────────┐  │
  │   │  DevTracker.Desktop                                              │  │
  │   │  [Avalonia UI — .NET 9 / C# 13]                                  │  │
  │   │                                                                   │  │
  │   │  Processo desktop cross-platform (Windows / Linux / macOS).      │  │
  │   │  Responsável pela interface gráfica MVVM e pelo ponto de         │  │
  │   │  entrada da aplicação (Program.cs, DI bootstrap).                │  │
  │   │                                                                   │  │
  │   │  Contém:                                                          │  │
  │   │  - Views (AXAML)                                                  │  │
  │   │  - ViewModels (CommunityToolkit.Mvvm)                             │  │
  │   │  - NavigationService, DialogService                               │  │
  │   │  - ViewLocator                                                    │  │
  │   └────────────────────────────┬────────────────────────────────────┘  │
  │                                │ chama via DI                           │
  │                                ▼                                        │
  │   ┌─────────────────────────────────────────────────────────────────┐  │
  │   │  DevTracker.Application                                          │  │
  │   │  [Class Library — .NET 9 / C# 13]                                │  │
  │   │                                                                   │  │
  │   │  Camada de lógica de negócio e orquestração.                     │  │
  │   │  Não tem dependências de UI nem de infraestrutura.               │  │
  │   │                                                                   │  │
  │   │  Contém:                                                          │  │
  │   │  - Interfaces de serviços (IProjectService, IWorkItemService…)   │  │
  │   │  - Interfaces de infraestrutura (IWorkspaceService, IAuthService…)│  │
  │   │  - DTOs (requests, responses, summaries)                         │  │
  │   │  - Validadores FluentValidation                                   │  │
  │   │  - Modelos de sessão e autenticação                               │  │
  │   │  - Definição de Permissions (strings estáveis)                   │  │
  │   │  - AppSettings e ISettingsService                                 │  │
  │   └────────────────────────────┬────────────────────────────────────┘  │
  │                                │ implementado por                       │
  │                                ▼                                        │
  │   ┌─────────────────────────────────────────────────────────────────┐  │
  │   │  DevTracker.Infrastructure                                       │  │
  │   │  [Class Library — .NET 9 / C# 13]                                │  │
  │   │                                                                   │  │
  │   │  Implementações concretas de persistência, segurança e IO.       │  │
  │   │                                                                   │  │
  │   │  Contém:                                                          │  │
  │   │  - AppDbContext (EF Core + SQLite)                                │  │
  │   │  - Migrations                                                     │  │
  │   │  - AuthService, PermissionService, SessionService                 │  │
  │   │  - WorkspaceService (System.IO)                                   │  │
  │   │  - SettingsService (System.Text.Json)                             │  │
  │   │  - Argon2PasswordHasher                                           │  │
  │   │  - Serilog configuration                                          │  │
  │   └───────────┬─────────────────────────┬──────────────────────────┘  │
  │               │                         │                               │
  │               ▼                         ▼                               │
  │   ┌───────────────────────┐   ┌─────────────────────────────────────┐  │
  │   │  DevTracker.Core      │   │  Storage                            │  │
  │   │  [Class Library]      │   │                                     │  │
  │   │                       │   │  ┌──────────────┐  ┌─────────────┐  │  │
  │   │  Domínio puro.        │   │  │  data.db     │  │ config.json │  │  │
  │   │  Sem dependências     │   │  │  [SQLite]    │  │ [JSON file] │  │  │
  │   │  externas.            │   │  └──────────────┘  └─────────────┘  │  │
  │   │                       │   │  ┌───────────────────────────────┐  │  │
  │   │  Contém:              │   │  │  Workspace Folder             │  │  │
  │   │  - Entidades          │   │  │  [Sistema de ficheiros local] │  │  │
  │   │  - Enums              │   │  └───────────────────────────────┘  │  │
  │   │  - BaseEntity         │   │  ┌───────────────────────────────┐  │  │
  │   └───────────────────────┘   │  │  logs/                        │  │  │
  │                               │  │  [Serilog file sink]          │  │  │
  │                               │  └───────────────────────────────┘  │  │
  │                               └─────────────────────────────────────┘  │
  └─────────────────────────────────────────────────────────────────────────┘
```

### Dependências entre containers

```
Desktop     →  Application  (chama interfaces de serviços)
Desktop     →  Infrastructure  (para DI bootstrap em Program.cs)
Application ←  Infrastructure  (Infrastructure implementa interfaces de Application)
Application →  Core  (usa entidades e enums)
Infrastructure → Core  (usa entidades para EF Core)
Infrastructure → SQLite  (via EF Core)
Infrastructure → FileSystem  (via System.IO)
Infrastructure → config.json  (via System.Text.Json)
```

> **Regra de dependência:** `Core` não conhece ninguém. `Application` conhece apenas `Core`.  
> `Infrastructure` conhece `Application` e `Core`. `Desktop` conhece `Application` e `Infrastructure` (apenas para DI).

---

## Nível 3 — Component Diagram

> **Os componentes internos de cada container.**

---

### 3.1 DevTracker.Desktop — Componentes

```
╔══════════════════════════════════════════════════════════════════════════╗
║  DevTracker.Desktop                                                      ║
╚══════════════════════════════════════════════════════════════════════════╝

  ┌─────────────────────────────────────────────────────────────────────┐
  │  Program.cs / App.axaml.cs                                          │
  │  [Bootstrap & DI Composition Root]                                  │
  │                                                                     │
  │  - Constrói o IServiceProvider                                      │
  │  - Lê WorkspaceRoot de config.json antes do DI (AppPaths)          │
  │  - Regista todos os serviços, VMs e serviços de navegação           │
  │  - Aplica EF migrations em runtime                                  │
  │  - Determina fluxo inicial: FirstRun / Login / Dashboard            │
  └──────────────────────────┬──────────────────────────────────────────┘
                             │
          ┌──────────────────┼──────────────────┐
          │                  │                  │
          ▼                  ▼                  ▼
  ┌───────────────┐  ┌────────────────┐  ┌─────────────────────┐
  │  ViewLocator  │  │ NavigationSvc  │  │  DialogService      │
  │               │  │                │  │                     │
  │  Liga VMs a   │  │ INavigation    │  │  IDialogService     │
  │  Views por    │  │ Service        │  │                     │
  │  convenção    │  │                │  │  Abre dialogs       │
  │  de namespace │  │  Navega entre  │  │  modais via         │
  │               │  │  PageViewModels│  │  Avalonia Window    │
  └───────────────┘  │  com histórico │  └─────────────────────┘
                     └────────────────┘

  ┌─────────────────────────────────────────────────────────────────────┐
  │  ViewModels                                                         │
  ├─────────────────┬───────────────────────────────────────────────────┤
  │  Shell          │  ShellViewModel                                   │
  │                 │  Controla página activa, estado de auth,          │
  │                 │  permissões da sidebar, logout                    │
  ├─────────────────┼───────────────────────────────────────────────────┤
  │  Auth           │  LoginViewModel                                   │
  │                 │  FirstRunSetupViewModel                           │
  │                 │  ReAuthenticateDialogViewModel                    │
  ├─────────────────┼───────────────────────────────────────────────────┤
  │  Projects       │  ProjectListViewModel                             │
  │                 │  ProjectDetailViewModel                           │
  │                 │  CreateProjectDialogViewModel                     │
  ├─────────────────┼───────────────────────────────────────────────────┤
  │  WorkItems      │  WorkItemListViewModel                            │
  │                 │  WorkItemDetailViewModel                          │
  │                 │  CreateWorkItemDialogViewModel                    │
  ├─────────────────┼───────────────────────────────────────────────────┤
  │  Repositories   │  RepositoryListViewModel                          │
  │                 │  CreateRepositoryDialogViewModel                  │
  ├─────────────────┼───────────────────────────────────────────────────┤
  │  Users          │  UserListViewModel                                │
  │                 │  CreateUserDialogViewModel                        │
  ├─────────────────┼───────────────────────────────────────────────────┤
  │  Audit          │  AuditLogViewModel                                │
  ├─────────────────┼───────────────────────────────────────────────────┤
  │  Settings       │  SettingsViewModel                                │
  └─────────────────┴───────────────────────────────────────────────────┘

  ┌─────────────────────────────────────────────────────────────────────┐
  │  Views (AXAML)                                                      │
  │  Uma View por ViewModel, ligada via ViewLocator por convenção       │
  │  de namespace (ViewModels.X → Views.X).                             │
  │  Compiled bindings (x:DataType) obrigatório.                       │
  │  Zero lógica de negócio em code-behind.                             │
  └─────────────────────────────────────────────────────────────────────┘
```

---

### 3.2 DevTracker.Application — Componentes

```
╔══════════════════════════════════════════════════════════════════════════╗
║  DevTracker.Application                                                  ║
╚══════════════════════════════════════════════════════════════════════════╝

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Abstractions/Services  [Interfaces de serviços de domínio]          │
  │                                                                      │
  │  IProjectService      — CRUD projetos, change state, archive         │
  │  IWorkItemService     — CRUD work items, change status, comentários  │
  │  IRepositoryService   — CRUD repositórios, attach                    │
  │  IUserService         — CRUD utilizadores, change role               │
  │  IAuditService        — Log imutável de eventos (só INSERT)          │
  │  ILabelService        — Gestão de labels e associação a work items   │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Abstractions/Security  [Interfaces de segurança]                    │
  │                                                                      │
  │  IAuthService                — Login, logout, bootstrap, mudança pwd │
  │  ISessionService             — Sessão em memória, timeout, lock      │
  │  IPasswordHasher             — Hash e verify Argon2id                │
  │  ICurrentUserService         — Utilizador autenticado atual          │
  │  IReAuthenticationService    — Confirmação de password               │
  │  IPermissionService          — Verifica permissão por operação       │
  │  IPermissionSnapshotService  — Snapshot de permissões para UI        │
  │  ICriticalActionTokenService — Token single-use para ops destrutivas │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Abstractions/IO  [Interfaces de sistema de ficheiros]               │
  │                                                                      │
  │  IWorkspaceService  — IO seguro de pastas (create/rename/archive)    │
  │  IAppPaths          — Paths resolvidos da aplicação                  │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Abstractions/Navigation  [Interfaces de navegação]                  │
  │                                                                      │
  │  INavigationService  — Navegação entre PageViewModels                │
  │  IDialogService      — Abertura de dialogs modais                    │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Abstractions/Settings                                               │
  │                                                                      │
  │  ISettingsService  — Carregar/guardar AppSettings (config.json)      │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  DTOs                                                                │
  │                                                                      │
  │  Projects/    — CreateProjectRequest, ProjectSummaryDto, …           │
  │  WorkItems/   — CreateWorkItemRequest, WorkItemDetailDto, …          │
  │  Repositories/— CreateRepositoryRequest, RepositorySummaryDto, …    │
  │  Users/       — CreateUserRequest, UserSummaryDto, …                 │
  │  Audit/       — AuditEntryDto                                        │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Validators  [FluentValidation]                                      │
  │                                                                      │
  │  Projects/    — CreateProjectRequestValidator                        │
  │               — ChangeProjectStateRequestValidator (razão obrigatória│
  │                  em Paused/Cancelled)                                │
  │  WorkItems/   — CreateWorkItemRequestValidator                       │
  │               — AddCommentRequestValidator                           │
  │  Users/       — CreateUserRequestValidator (pwd policy, não-Owner)   │
  │  Settings/    — AppSettingsValidator                                 │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Security                                                            │
  │                                                                      │
  │  Permissions  — Constantes estáticas de operações (ex: "project.create")│
  │  Models/      — UserSession, SessionInfo, AuthResult, Result<T>      │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Settings                                                            │
  │                                                                      │
  │  AppSettings    — Modelo de configuração persistida                  │
  │  UiPreferences  — Tema, língua, preferências de UI                   │
  └──────────────────────────────────────────────────────────────────────┘
```

---

### 3.3 DevTracker.Infrastructure — Componentes

```
╔══════════════════════════════════════════════════════════════════════════╗
║  DevTracker.Infrastructure                                               ║
╚══════════════════════════════════════════════════════════════════════════╝

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Persistence                                                         │
  │                                                                      │
  │  AppDbContext               — DbContext EF Core com SQLite           │
  │                               Override SaveChanges para UpdatedAt    │
  │                               ApplyConfigurationsFromAssembly        │
  │                                                                      │
  │  Configurations/            — Fluent API por entidade:               │
  │    UserConfiguration        — índice único em Username               │
  │    ProjectConfiguration     — QueryFilter soft delete, color max 7   │
  │    WorkItemConfiguration    — QueryFilter, índice único ProjectId+Num │
  │    CommentConfiguration     — QueryFilter soft delete                │
  │    WorkItemLabelConfiguration— chave composta (WorkItemId, LabelId)  │
  │    AuditEntryConfiguration  — SetNull em UserId, índices por data    │
  │                                                                      │
  │  Migrations/                — EF Core Migrations versionadas         │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Security                                                            │
  │                                                                      │
  │  AuthService                — Login, logout, setup owner, change pwd │
  │                               Implementa IAuthService +              │
  │                               IReAuthenticationService               │
  │  InMemorySessionService     — Sessão em RAM, timeout 15min, lock     │
  │  CurrentUserService         — Wrapper do ISessionService para UI     │
  │  Argon2PasswordHasher       — Argon2id com salt aleatório por user   │
  │  PermissionService          — Matriz Role→Permissions, BD lookup     │
  │  PermissionSnapshotService  — Cache de permissões por scope para UI  │
  │  InMemoryCriticalAction     — Token single-use 30s para ops          │
  │    TokenService               destrutivas                            │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Services  [Implementações de IXxxService]                           │
  │                                                                      │
  │  ProjectService    — CRUD, ChangeState (cria StateTransition),       │
  │                      Archive, Delete (soft)                          │
  │  WorkItemService   — CRUD, número sequencial por projeto,            │
  │                      ChangeStatus, AddComment                        │
  │  RepositoryService — CRUD, Attach (chama WorkspaceService)           │
  │  UserService       — CRUD, ChangeRole, proteção último Owner         │
  │  AuditService      — INSERT em AuditEntry, queries por entidade/user │
  │  LabelService      — CRUD labels, attach/detach a work items         │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  IO                                                                  │
  │                                                                      │
  │  WorkspaceService     — IO seguro de pastas, sanitização de nomes,  │
  │                         path traversal prevention, archive para      │
  │                         _archive/, cross-volume copy+delete          │
  │  FileNameSanitizer    — Regex para nomes de pasta seguros            │
  │  AppPaths             — Resolve todos os paths da app, incluindo     │
  │                         ReadWorkspaceRootFromConfig() para bootstrap │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Settings                                                            │
  │                                                                      │
  │  SettingsService  — Lê/escreve config.json via System.Text.Json     │
  │                     Defaults seguros em caso de ficheiro corrompido  │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  DependencyInjection  [Extension methods]                            │
  │                                                                      │
  │  AddInfrastructure(dbPath)     — EF Core + SQLite                   │
  │  AddSecurity()                 — Auth, Session, Hasher, Tokens       │
  │  AddAuthorization()            — PermissionService, Snapshot         │
  │  AddFileSystem(workspaceRoot)  — WorkspaceService, AppPaths          │
  │  AddDomainServices()           — Todos os IXxxService                │
  │  AddSettings()                 — SettingsService                     │
  └──────────────────────────────────────────────────────────────────────┘
```

---

### 3.4 DevTracker.Core — Componentes

```
╔══════════════════════════════════════════════════════════════════════════╗
║  DevTracker.Core                                                         ║
╚══════════════════════════════════════════════════════════════════════════╝

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Entities                                                            │
  │                                                                      │
  │  Base/BaseEntity  — Id (Guid), CreatedAt, UpdatedAt                  │
  │                                                                      │
  │  User             — Username, PasswordHash, Salt, Role, IsActive     │
  │  Project          — Name, Color, WorkspacePath, State, soft delete   │
  │  Repository       — RelativePath, soft delete                        │
  │  WorkItem         — Number, Title, Type, Status, Priority, soft del  │
  │  Comment          — Body, AuthorUserId, EditedAt, soft delete        │
  │  Label            — Name, Color                                      │
  │  WorkItemLabel    — Join table (WorkItemId, LabelId)                 │
  │  StateTransition  — FromState, ToState, Reason, ChangedAt            │
  │                     (não herda BaseEntity — evento imutável)         │
  │  AuditEntry       — Action, EntityType, EntityId, OldValue, NewValue │
  │                     (só INSERT — nunca UPDATE/DELETE)                │
  └──────────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────────┐
  │  Enums                                                               │
  │                                                                      │
  │  UserRole          — Owner, Admin, Maintainer, Reader                │
  │  ProjectState      — NotStarted, InProgress, Paused, Cancelled,      │
  │                       Completed                                      │
  │  WorkItemType      — Task, Feature, Bug, Improvement, Documentation  │
  │  WorkItemStatus    — Backlog, Todo, InProgress, InReview, Done,      │
  │                       Cancelled                                      │
  │  WorkItemPriority  — Low, Medium, High, Critical                     │
  │  AuditAction       — Login/Logout/PasswordChanged (1xx),             │
  │                       Project* (2xx), Repository* (3xx),             │
  │                       WorkItem* (4xx), User* (5xx)                   │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## Fluxos transversais

### Fluxo de autenticação e arranque

```
[Program.cs]
    │
    ├── 1. Ler WorkspaceRoot de config.json  (AppPaths.ReadWorkspaceRootFromConfig)
    ├── 2. Construir AppPaths + DI container
    ├── 3. Aplicar EF Migrations             (db.Database.MigrateAsync)
    ├── 4. ISettingsService.IsInitializedAsync()
    │         false → FirstRunSetupView (definir workspace)
    │         true  → continuar
    ├── 5. IAuthService.AnyUsersExistAsync()
    │         false → FirstRunSetupView (criar Owner)
    │         true  → LoginView
    └── 6. Login com sucesso → ProjectListView (sessão em RAM)
```

### Fluxo de operação destrutiva (ex: apagar projeto)

```
[ViewModel]
    │
    ├── 1. IDialogService.ConfirmAsync("Apagar projeto?")
    ├── 2. IDialogService.ShowDialogAsync<ReAuthenticateDialogViewModel>()
    │         └── IReAuthenticationService.ConfirmPasswordAsync(userId, pwd)
    ├── 3. ICriticalActionTokenService.CreateToken(userId, "project.delete", 30s)
    ├── 4. IProjectService.DeleteAsync(projectId, criticalToken)
    │         └── IPermissionService.EnsureCanPerformAsync(userId, "project.delete")
    │         └── ICriticalActionTokenService.ConsumeToken(token, userId, "project.delete")
    │         └── IWorkspaceService.ArchiveProjectFolderAsync(projectId, path)
    │         └── Project.IsDeleted = true  →  SaveChangesAsync
    │         └── IAuditService.LogAsync(ProjectDeleted, …)
    └── 5. Recarregar lista de projetos
```

### Fluxo de permissões na UI

```
[OnActivatedAsync no ViewModel]
    │
    └── IPermissionSnapshotService.GetCurrentUserPermissionsAsync()
              (HashSet<string> em memória — sem query à BD)
              │
              ├── CanCreate  = permissions.Contains("project.create")
              ├── CanDelete  = permissions.Contains("project.delete")
              └── CanArchive = permissions.Contains("project.archive")
                        │
                        └── Bindings AXAML: IsVisible, Command.CanExecute
                                  (apenas UX — autorização real nos serviços)
```

---

## Resumo de responsabilidades por camada

| Camada | Responsabilidade | Não deve fazer |
|---|---|---|
| **Core** | Entidades, enums, value objects | Depender de qualquer outra camada |
| **Application** | Interfaces, DTOs, validadores, modelos de sessão | IO, BD, UI |
| **Infrastructure** | EF Core, SQLite, IO, Argon2id, Serilog, JSON | Lógica de negócio |
| **Desktop** | UI AXAML, ViewModels, navegação, bootstrap DI | IO direto, lógica de negócio |
