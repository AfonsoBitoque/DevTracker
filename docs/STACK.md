# DevTracker — Stack & Architecture Context

> Ficheiro de contexto técnico para uso com Windsurf AI / Cursor / Copilot.
> Última revisão: Junho 2026

---

## 1. Visão Geral do Projeto

**DevTracker** é uma aplicação desktop offline para gestão pessoal de projetos de software.  
Funcionalidades principais:
- Gestão de repositórios/projetos com criação real de pastas no sistema de ficheiros
- Gestor de tarefas integrado (estilo Jira) associado a cada projeto
- Estados de projeto com máquina de estados, razões para transições, e histórico de auditoria
- Sistema de autenticação local com roles e permissões — todas as operações críticas exigem autenticação prévia
- 100% offline, dados locais, sem dependência de serviços cloud

---

## 2. Stack Tecnológica

### 2.1 Framework Desktop — Avalonia UI

| Propriedade        | Valor                            |
|--------------------|----------------------------------|
| Framework          | **Avalonia UI 11.x** (.NET 9)    |
| Paradigma UI       | MVVM (Model-View-ViewModel)      |
| Linguagem          | C# 13                            |
| Plataformas alvo   | Windows, Linux, macOS            |
| Por que Avalonia?  | Cross-platform real (inclui Linux), API semelhante a WPF, madura e estável em 2026 |

**Packages Avalonia principais:**
```xml
<PackageReference Include="Avalonia" Version="11.*" />
<PackageReference Include="Avalonia.Desktop" Version="11.*" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
```

### 2.2 Persistência — EF Core + SQLite

| Propriedade        | Valor                                      |
|--------------------|--------------------------------------------|
| ORM                | **Entity Framework Core 9**                |
| Base de dados      | **SQLite** (ficheiro local na máquina)     |
| Migrações          | EF Core Migrations (versionamento do schema)|
| Caminho da BD      | `Environment.SpecialFolder.LocalApplicationData` + `DevTracker/data.db` |

**Packages:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*" />
```

### 2.3 Segurança e Autenticação Local

| Propriedade           | Valor                                              |
|-----------------------|----------------------------------------------------|
| Hash de passwords     | **Argon2id** via `Isopoh.Cryptography.Argon2`      |
| Proteção de dados     | **DPAPI / SecretService** via `Microsoft.AspNetCore.DataProtection` adaptado a desktop |
| Gestão de segredos    | Chaves cifradas em ficheiro separado (`secrets.db`) |
| Sessão               | Token de sessão em memória RAM (não persiste em disco) |
| Inatividade           | Lock automático após X minutos (configurável, ver SETTINGS.md) |
| Re-auth em operações críticas | Diálogo de confirmação de password para: apagar projeto, apagar repo, mudar role |

**Packages:**
```xml
<PackageReference Include="Isopoh.Cryptography.Argon2" Version="2.*" />
<PackageReference Include="Microsoft.AspNetCore.DataProtection" Version="9.*" />
```

> **Nota sobre DataProtection em desktop:** `Microsoft.AspNetCore.DataProtection` requer bootstrap manual sem host ASP.NET.
> Usar `DataProtectionProvider.Create(directoryPath)` ou o método de extensão `AddDataProtection()` com `IServiceCollection`.
> Não é necessário referenciar `Microsoft.AspNetCore.App` completo — o pacote NuGet standalone é suficiente.

### 2.4 Validação

```xml
<PackageReference Include="FluentValidation" Version="11.*" />
```

### 2.5 Logging e Diagnóstico

```xml
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Sinks.File" Version="5.*" />
```
Logs em `LocalApplicationData/DevTracker/logs/`. Nunca logar passwords ou tokens.

### 2.6 Configuração persistida

```xml
<!-- Incluído no .NET runtime, sem pacote extra -->
<!-- System.Text.Json para serialização do config.json -->
```
Ver SETTINGS.md para o modelo completo de `AppSettings` e `ISettingsService`.

### 2.7 Testes

```xml
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="Bogus" Version="35.*" />
```
Ver TESTING.md para estratégia completa, convenções e exemplos.

---

## 3. Estrutura da Solution

```
DevTracker.sln
│
├── src/
│   ├── DevTracker.Core/          # Domínio puro: entidades, interfaces, enums, value objects
│   ├── DevTracker.Application/   # Casos de uso, serviços de aplicação, DTOs, validadores
│   ├── DevTracker.Infrastructure/# EF Core, SQLite, repositórios, WorkspaceService (System.IO), Serilog
│   └── DevTracker.Desktop/       # Projeto Avalonia UI: Views, ViewModels, Program.cs
│
└── tests/
    ├── DevTracker.Core.Tests/
    └── DevTracker.Application.Tests/
```

### Dependências entre projetos
```
Desktop → Application → Core
Infrastructure → Application → Core
(Infrastructure implementa interfaces definidas em Core)
```

---

## 4. Modelo de Domínio (Core)

### Entidades Principais

```csharp
// User & Auth
User            { Id, Username, PasswordHash, Salt, Role, CreatedAt, LastLoginAt }
Role            { Owner | Admin | Maintainer | Reader }
Session         { Token (in-memory only), UserId, ExpiresAt }
AuditEntry      { Id, UserId, Action, EntityType, EntityId, OldValue, NewValue, Timestamp }

// Projetos
Project         { Id, Name, Description, Color, WorkspacePath, State, CreatedAt, UpdatedAt }
ProjectState    { NotStarted | InProgress | Paused | Cancelled | Completed }
StateTransition { Id, ProjectId, FromState, ToState, Reason, ChangedByUserId, ChangedAt }
Repository      { Id, ProjectId, Name, RelativePath, CreatedAt }  // caminho relativo ao WorkspacePath

// Tarefas (Task Manager tipo Jira)
WorkItem        { Id, ProjectId, Title, Description, Type, Status, Priority, AssignedToUserId, CreatedByUserId, CreatedAt, UpdatedAt, DueDate }
WorkItemType    { Feature | Bug | Task | Improvement | Documentation }
WorkItemStatus  { Backlog | Todo | InProgress | InReview | Done | Cancelled }
WorkItemPriority{ Critical | High | Medium | Low }
Comment         { Id, WorkItemId, AuthorUserId, Body, CreatedAt, EditedAt }
Label           { Id, Name, Color }
WorkItemLabel   { WorkItemId, LabelId }  // many-to-many
```

### Value Objects

```csharp
WorkspacePath   // wrapper para string, valida que o path existe e é acessível
ProjectColor    // hex string validada
```

---

## 5. Serviços de Aplicação (Application)

```csharp
IAuthService          // Login, Logout, ValidateSession, ChangePassword, ReAuthenticate
IProjectService       // CreateProject, ArchiveProject, ChangeState (com Reason), GetAll, GetById
IWorkspaceService     // CreateRepositoryFolder, AttachExistingFolder, RenameFolder, DeleteFolder (com soft-delete)
IWorkItemService      // CreateWorkItem, UpdateStatus, AssignTo, AddComment, GetByProject
IAuditService         // LogAction (chamado internamente pelos outros serviços)
IPermissionService    // CanPerform(userId, operation) → bool
```

**Regra de ouro:** Toda a lógica de negócio e IO fica nos serviços. Os ViewModels chamam serviços, nunca System.IO diretamente.

---

## 6. Permissões por Role

| Operação                        | Owner | Admin | Maintainer | Reader |
|---------------------------------|-------|-------|------------|--------|
| Criar projeto                   | ✅    | ✅    | ❌         | ❌     |
| Apagar projeto                  | ✅    | ❌    | ❌         | ❌     |
| Mudar estado projeto            | ✅    | ✅    | ✅         | ❌     |
| Criar repositório (pasta)       | ✅    | ✅    | ✅         | ❌     |
| Apagar repositório (pasta)      | ✅    | ✅    | ❌         | ❌     |
| Criar/editar work items         | ✅    | ✅    | ✅         | ❌     |
| Comentar work items             | ✅    | ✅    | ✅         | ✅     |
| Gerir utilizadores              | ✅    | ❌    | ❌         | ❌     |
| Ver audit log                   | ✅    | ✅    | ❌         | ❌     |

---

## 7. Workspace e Sistema de Ficheiros

- O utilizador define um **workspace root** na primeira execução (ex: `~/DevTracker/`)
- Todos os repositórios são criados **dentro** dessa pasta raiz — a app nunca toca fora dela
- Estrutura de pastas criada:
  ```
  ~/DevTracker/
  ├── ProjectA/
  │   ├── repo-backend/
  │   └── repo-frontend/
  └── ProjectB/
      └── lib-core/
  ```
- O `IWorkspaceService` valida que todos os paths estão dentro do workspace root antes de qualquer IO

---

## 8. Segurança — Regras de Implementação

1. **Nunca guardar password em plaintext.** Sempre Argon2id com salt único por utilizador.
2. **Sessão 100% em memória.** Token não vai a disco, expira com fecho da app ou timeout.
3. **Re-autenticação para operações destrutivas.** Apagar projetos/repos exige confirmação de password.
4. **Audit log imutável.** `AuditEntry` só tem INSERT, nunca UPDATE/DELETE.
5. **Validar inputs em FluentValidation** antes de qualquer operação de serviço.
6. **Princípio do menor privilégio.** Verificar `IPermissionService.CanPerform()` no início de cada método de serviço, não na UI.
7. **Não logar dados sensíveis.** Serilog nunca regista passwords, tokens ou conteúdo de ficheiros.

---

## 9. Padrões de Código

- **MVVM estrito:** ViewModels herdam de `ObservableObject` (CommunityToolkit.Mvvm). Sem code-behind.
- **Injeção de dependências:** Microsoft.Extensions.DependencyInjection registado no `Program.cs`.
- **Async/await everywhere:** Todos os métodos de serviço que tocam BD ou disco são `async Task<T>`.
- **Result pattern:** Serviços retornam `Result<T>` (ou similar) em vez de lançar exceções para erros esperados.
- **Migrations:** Sempre via `dotnet ef migrations add <Name>`. Nunca alterar schema manualmente.

---

## 10. Configuração e Paths

```csharp
// Paths padrão em runtime
string appData    = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
string appFolder  = Path.Combine(appData, "DevTracker");
string dbPath     = Path.Combine(appFolder, "data.db");
string secretsDb  = Path.Combine(appFolder, "secrets.db");
string logsPath   = Path.Combine(appFolder, "logs");
string configPath = Path.Combine(appFolder, "config.json");
```

Em Linux: `~/.local/share/DevTracker/`  
Em Windows: `C:\Users\<user>\AppData\Local\DevTracker\`  
Em macOS: `~/Library/Application Support/DevTracker/`

---

## 11. Comandos de Setup (primeira vez)

```bash
# Criar solution e projetos
dotnet new sln -n DevTracker
dotnet new classlib -n DevTracker.Core -o src/DevTracker.Core
dotnet new classlib -n DevTracker.Application -o src/DevTracker.Application
dotnet new classlib -n DevTracker.Infrastructure -o src/DevTracker.Infrastructure
dotnet new avalonia.mvvm -n DevTracker.Desktop -o src/DevTracker.Desktop

# Adicionar à solution
dotnet sln add src/DevTracker.Core/
dotnet sln add src/DevTracker.Application/
dotnet sln add src/DevTracker.Infrastructure/
dotnet sln add src/DevTracker.Desktop/

# Referências entre projetos
dotnet add src/DevTracker.Application/ reference src/DevTracker.Core/
dotnet add src/DevTracker.Infrastructure/ reference src/DevTracker.Application/
dotnet add src/DevTracker.Desktop/ reference src/DevTracker.Application/
dotnet add src/DevTracker.Desktop/ reference src/DevTracker.Infrastructure/

# Instalar template Avalonia (se necessário)
dotnet new install Avalonia.Templates

# Primeira migração EF
dotnet ef migrations add InitialCreate --project src/DevTracker.Infrastructure --startup-project src/DevTracker.Desktop
dotnet ef database update --project src/DevTracker.Infrastructure --startup-project src/DevTracker.Desktop
```

---

## 12. Ficheiros de Contexto — Estado Atual

| Ficheiro           | Estado   | Conteúdo                                                          |
|--------------------|----------|-------------------------------------------------------------------|
| `DOMAIN.md`        | ✅ Criado | Entidades EF Core, DbContext, configurações Fluent API            |
| `AUTH.md`          | ✅ Criado | AuthService, sessão em memória, Argon2id, re-autenticação        |
| `WORKSPACE.md`     | ✅ Criado | WorkspaceService, IO seguro, path traversal, archive             |
| `PERMISSIONS.md`   | ✅ Criado | IPermissionService, matriz de permissões, uso nos serviços       |
| `VIEWMODELS.md`    | ✅ Criado | ViewModels, MVVM, DI em Avalonia, ViewLocator, ciclo de vida     |
| `SERVICES.md`      | ✅ Criado | Interfaces, DTOs, validadores FluentValidation, implementações   |
| `SETTINGS.md`      | ✅ Criado | AppSettings, ISettingsService, bootstrap do workspace            |
| `TESTING.md`       | ✅ Criado | Estratégia de testes, SQLite in-memory, exemplos por camada      |
| `NAVIGATION.md`    | ✅ Criado | INavigationService, IDialogService, fluxos de navegação          |

