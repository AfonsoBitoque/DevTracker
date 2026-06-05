# DevTracker — Workspace & File System Context

> Ficheiro de contexto: gestão segura do workspace local, criação de pastas, validação de paths e operações de IO.
> Usar em conjunto com STACK.md, DOMAIN.md e AUTH.md.

---

## 1. Objetivo

O DevTracker precisa de criar e gerir pastas reais no computador do utilizador.  
Como isso implica permissões elevadas sobre o sistema de ficheiros, **toda a IO tem de ser centralizada, validada e auditada**.

Este ficheiro define:
- o conceito de `WorkspaceRoot`
- como criar projetos e repositórios físicos em segurança
- como evitar path traversal e acessos fora da zona permitida
- como apagar, renomear e anexar pastas existentes
- como listar e copiar conteúdo de diretórios
- como integrar permissões, autenticação e auditoria nestas operações

---

## 2. Regra principal

**A aplicação nunca deve fazer IO arbitrária fora do `WorkspaceRoot` configurado pelo utilizador.**

Tudo o que seja criação, renomeação, leitura estrutural, delete, attach ou cópia de diretórios deve passar por um único serviço:

```csharp
IWorkspaceService
```

Nenhum ViewModel, helper de UI, ou outro serviço deve usar `Directory.*`, `File.*`, `Path.*` para operações de negócio sem passar por este serviço.

---

## 3. Conceitos principais

### WorkspaceRoot

Pasta raiz escolhida pelo utilizador na primeira configuração da app.

Exemplos:
- Linux: `/home/afonso/DevTracker`
- Windows: `C:\Users\Afonso\DevTracker`

### Project folder

Cada `Project` tem uma pasta principal própria dentro do workspace.

Exemplo:
```text
/home/afonso/DevTracker/MyProject
```

### Repository folder

Cada `Repository` é uma subpasta física dentro da pasta do projeto.

Exemplo:
```text
/home/afonso/DevTracker/MyProject/backend
/home/afonso/DevTracker/MyProject/frontend
```

---

## 4. Estrutura esperada no disco

```text
WorkspaceRoot/
├── Project-A/
│   ├── backend/
│   ├── frontend/
│   └── docs/
├── Project-B/
│   └── core-lib/
└── _archive/
```

### Pasta `_archive`

Em vez de fazer delete destrutivo imediato, repositórios e projetos podem ser movidos para:

```text
WorkspaceRoot/_archive/
```

Isto permite uma estratégia de **soft-delete físico** antes da remoção permanente.

---

## 5. Interfaces

### IWorkspaceService

```csharp
// src/DevTracker.Application/Abstractions/IO/IWorkspaceService.cs
using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.IO;

public interface IWorkspaceService
{
    Task<Result<string>> EnsureWorkspaceRootAsync(string path, CancellationToken ct = default);

    Task<Result<string>> CreateProjectFolderAsync(Guid projectId, string projectName, CancellationToken ct = default);
    Task<Result<string>> RenameProjectFolderAsync(Guid projectId, string currentAbsolutePath, string newProjectName, CancellationToken ct = default);
    Task<Result> ArchiveProjectFolderAsync(Guid projectId, string currentAbsolutePath, CancellationToken ct = default);

    Task<Result<string>> CreateRepositoryFolderAsync(Guid projectId, string projectAbsolutePath, string repositoryName, CancellationToken ct = default);
    Task<Result<string>> RenameRepositoryFolderAsync(Guid repositoryId, string repositoryAbsolutePath, string newRepositoryName, CancellationToken ct = default);
    Task<Result> ArchiveRepositoryFolderAsync(Guid repositoryId, string repositoryAbsolutePath, CancellationToken ct = default);

    Task<Result<string>> AttachExistingFolderAsync(Guid projectId, string sourceAbsolutePath, CancellationToken ct = default);

    /// <summary>Lista ficheiros e pastas num path absoluto (read-only).</summary>
    Task<Result<IReadOnlyList<WorkspaceEntry>>> ListEntriesAsync(string absolutePath, CancellationToken ct = default);

    /// <summary>Copia conteúdo de source para dest (cross-device safe).</summary>
    Task<Result> CopyDirectoryContentsAsync(string sourceAbsolutePath, string destinationAbsolutePath, CancellationToken ct = default);

    bool IsInsideWorkspace(string absolutePath);
    string NormalizeAbsolutePath(string path);
}

public sealed record WorkspaceEntry(string Name, string FullPath, bool IsDirectory);
```

### Result genérico

```csharp
// src/DevTracker.Application/Common/ResultOfT.cs
namespace DevTracker.Application.Common;

public sealed record Result<T>(bool Succeeded, T? Value = default, string? Error = null)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
```

---

## 6. Regras de segurança para paths

### 6.1 Normalização obrigatória

Antes de qualquer operação:
- usar `Path.GetFullPath(...)`
- remover ambiguidades de `..`
- resolver separadores de pasta do SO atual
- nunca confiar em strings recebidas da UI

### 6.2 Garantir que o path está dentro do workspace

Exemplo correto:

```csharp
var fullPath = Path.GetFullPath(candidatePath);
var rootPath = Path.GetFullPath(_workspaceRoot);

if (!fullPath.StartsWith(rootPath, StringComparison.Ordinal))
    return Result.Fail("O caminho está fora do workspace permitido.");
```

### 6.3 Proibir nomes perigosos

Nomes de projeto e repositório não devem aceitar:
- `/`
- `\\`
- `..`
- `:`
- `*`
- `?`
- `"`
- `<`
- `>`
- `|`
- espaços no início/fim
- nomes vazios

### 6.4 Nunca usar path vindo diretamente da UI como autoridade

A UI pode propor um caminho, mas a decisão final é sempre reconstruída pelo serviço com base em:
- workspace root conhecido
- nome sanitizado
- IDs existentes na base de dados

---

## 7. Sanitização de nomes

```csharp
// src/DevTracker.Infrastructure/IO/FileNameSanitizer.cs
using System.Text.RegularExpressions;

namespace DevTracker.Infrastructure.IO;

public static partial class FileNameSanitizer
{
    [GeneratedRegex("[^a-zA-Z0-9._ -]")]
    private static partial Regex InvalidCharsRegex();

    public static string SanitizeFolderName(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Nome inválido.");

        trimmed = trimmed.Replace("..", "-");
        trimmed = InvalidCharsRegex().Replace(trimmed, "-");
        trimmed = Regex.Replace(trimmed, "\\s+", " ");

        return trimmed.Length > 80
            ? trimmed[..80].Trim()
            : trimmed;
    }
}
```

### Regras adicionais

- Pode ser útil converter espaços para `-` mais tarde, se quiseres naming estilo slug
- No arranque do projeto, manter espaços é aceitável para legibilidade
- O nome apresentado na UI pode ser diferente do nome físico da pasta no futuro

---

## 8. Implementação do WorkspaceService

```csharp
// src/DevTracker.Infrastructure/IO/WorkspaceService.cs
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Core.Enums;

namespace DevTracker.Infrastructure.IO;

public sealed class WorkspaceService(
    IPermissionService permissionService,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IAppPaths appPaths) : IWorkspaceService
{
    private string WorkspaceRoot => appPaths.WorkspaceRoot;

    public async Task<Result<string>> EnsureWorkspaceRootAsync(string path, CancellationToken ct = default)
    {
        var normalized = NormalizeAbsolutePath(path);
        Directory.CreateDirectory(normalized);
        Directory.CreateDirectory(Path.Combine(normalized, "_archive"));
        return Result<string>.Success(normalized);
    }

    public async Task<Result<string>> CreateProjectFolderAsync(Guid projectId, string projectName, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.ProjectCreate, ct);

        var fullPath = BuildPath(projectName);
        if (!IsInsideWorkspace(fullPath)) return Result<string>.Fail("Path inválido.");
        if (Directory.Exists(fullPath))   return Result<string>.Fail("Já existe uma pasta com esse nome.");

        Directory.CreateDirectory(fullPath);
        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectCreated, "ProjectFolder", projectId, null, fullPath, ct);
        return Result<string>.Success(fullPath);
    }

    public async Task<Result<string>> RenameProjectFolderAsync(Guid projectId, string currentPath, string newName, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Utilizador não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.ProjectRename, ct);

        return await RenameFolderAsync(currentPath, WorkspaceRoot, newName,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectUpdated, "ProjectFolder", projectId, currentPath, target, ct));
    }

    public async Task<Result> ArchiveProjectFolderAsync(Guid projectId, string currentPath, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Utilizador não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.ProjectDelete, ct);

        return await ArchiveFolderAsync(currentPath,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectDeleted, "ProjectFolder", projectId, currentPath, target, ct));
    }

    public async Task<Result<string>> CreateRepositoryFolderAsync(Guid projectId, string projectPath, string repoName, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Utilizador não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.RepositoryCreate, ct);

        var projectNorm = NormalizeAbsolutePath(projectPath);
        if (!IsInsideWorkspace(projectNorm) || !Directory.Exists(projectNorm))
            return Result<string>.Fail("Pasta de projeto inválida.");

        var safeName = FileNameSanitizer.SanitizeFolderName(repoName);
        var fullPath = NormalizeAbsolutePath(Path.Combine(projectNorm, safeName));
        if (!IsInsideWorkspace(fullPath)) return Result<string>.Fail("Path inválido.");
        if (Directory.Exists(fullPath))   return Result<string>.Fail("Já existe um repositório com esse nome.");

        Directory.CreateDirectory(fullPath);
        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryCreated, "RepositoryFolder", projectId, null, fullPath, ct);
        return Result<string>.Success(fullPath);
    }

    public async Task<Result<string>> RenameRepositoryFolderAsync(Guid repositoryId, string repoPath, string newName, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Utilizador não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.RepositoryRename, ct);

        var source = NormalizeAbsolutePath(repoPath);
        var parent = Directory.GetParent(source)?.FullName ?? string.Empty;
        return await RenameFolderAsync(repoPath, parent, newName,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryRenamed, "RepositoryFolder", repositoryId, repoPath, target, ct));
    }

    public async Task<Result> ArchiveRepositoryFolderAsync(Guid repositoryId, string repoPath, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Utilizador não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.RepositoryDelete, ct);

        return await ArchiveFolderAsync(repoPath,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryDeleted, "RepositoryFolder", repositoryId, repoPath, target, ct));
    }

    public async Task<Result<string>> AttachExistingFolderAsync(Guid projectId, string sourcePath, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Utilizador não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.RepositoryAttach, ct);

        var source = NormalizeAbsolutePath(sourcePath);
        if (!Directory.Exists(source)) return Result<string>.Fail("A pasta indicada não existe.");

        var safeName = FileNameSanitizer.SanitizeFolderName(Path.GetFileName(source));
        var target   = NormalizeAbsolutePath(Path.Combine(WorkspaceRoot, safeName));
        if (!IsInsideWorkspace(target))  return Result<string>.Fail("Destino inválido.");
        if (Directory.Exists(target))    return Result<string>.Fail("Já existe uma pasta com esse nome no workspace.");

        // Tenta Move (mesma partição). Fallback para Copy+Delete (cross-volume).
        try { Directory.Move(source, target); }
        catch (IOException)
        {
            CopyDirectoryRecursive(source, target, overwrite: false);
            Directory.Delete(source, recursive: true);
        }

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryCreated, "AttachedFolder", projectId, source, target, ct);
        return Result<string>.Success(target);
    }

    public Task<Result<IReadOnlyList<WorkspaceEntry>>> ListEntriesAsync(string absolutePath, CancellationToken ct = default)
    {
        var path = NormalizeAbsolutePath(absolutePath);
        if (!IsInsideWorkspace(path) || !Directory.Exists(path))
            return Task.FromResult(Result<IReadOnlyList<WorkspaceEntry>>.Success(Array.Empty<WorkspaceEntry>()));

        var entries = new List<WorkspaceEntry>();

        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
        {
            try { entries.Add(new WorkspaceEntry(Path.GetFileName(dir), dir, true)); }
            catch { /* Ignorar entradas que não podem ser lidas */ }
        }

        foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
        {
            try { entries.Add(new WorkspaceEntry(Path.GetFileName(file), file, false)); }
            catch { /* Ignorar entradas que não podem ser lidas */ }
        }

        return Task.FromResult(Result<IReadOnlyList<WorkspaceEntry>>.Success(entries));
    }

    public Task<Result> CopyDirectoryContentsAsync(string sourcePath, string destPath, CancellationToken ct = default)
    {
        var source = NormalizeAbsolutePath(sourcePath);
        var dest   = NormalizeAbsolutePath(destPath);

        if (!Directory.Exists(source)) return Task.FromResult(Result.Fail("Pasta de origem não existe."));
        if (!IsInsideWorkspace(dest))  return Task.FromResult(Result.Fail("Destino fora do workspace."));

        CopyDirectoryRecursive(source, dest, overwrite: true);
        return Task.FromResult(Result.Success());
    }

    public bool IsInsideWorkspace(string absolutePath)
    {
        var full = NormalizeAbsolutePath(absolutePath);
        var root = NormalizeAbsolutePath(WorkspaceRoot);
        return full.StartsWith(root, StringComparison.Ordinal);
    }

    public string NormalizeAbsolutePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // ── Helpers privados ──────────────────────────────────────────────────────

    private string BuildPath(string name)
        => NormalizeAbsolutePath(Path.Combine(WorkspaceRoot, FileNameSanitizer.SanitizeFolderName(name)));

    private string BuildArchivePath(string folderName)
    {
        var archiveRoot = NormalizeAbsolutePath(Path.Combine(WorkspaceRoot, "_archive"));
        Directory.CreateDirectory(archiveRoot);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return NormalizeAbsolutePath(Path.Combine(archiveRoot, $"{FileNameSanitizer.SanitizeFolderName(folderName)}-{timestamp}"));
    }

    private async Task<Result<string>> RenameFolderAsync(string sourcePath, string parentDir, string newName, Func<string, Task> auditLog)
    {
        var source = NormalizeAbsolutePath(sourcePath);
        if (!IsInsideWorkspace(source) || !Directory.Exists(source))
            return Result<string>.Fail("Pasta de origem inválida.");

        var safeName = FileNameSanitizer.SanitizeFolderName(newName);
        var target   = NormalizeAbsolutePath(Path.Combine(parentDir, safeName));
        if (!IsInsideWorkspace(target))  return Result<string>.Fail("Destino inválido.");
        if (Directory.Exists(target))    return Result<string>.Fail("Já existe uma pasta com esse nome.");

        Directory.Move(source, target);
        await auditLog(target);
        return Result<string>.Success(target);
    }

    private async Task<Result> ArchiveFolderAsync(string sourcePath, Func<string, Task> auditLog)
    {
        var source = NormalizeAbsolutePath(sourcePath);
        if (!IsInsideWorkspace(source) || !Directory.Exists(source))
            return Result.Fail("Pasta inválida.");

        var archivePath = BuildArchivePath(Path.GetFileName(source));
        Directory.Move(source, archivePath);
        await auditLog(archivePath);
        return Result.Success();
    }

    private static void CopyDirectoryRecursive(string src, string dest, bool overwrite = false)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectoryRecursive(dir, Path.Combine(dest, Path.GetFileName(dir)), overwrite);
    }
}
```

---

## 9. Bootstrap do WorkspaceRoot

O `WorkspaceRoot` não é um valor estático — é definido pelo utilizador na primeira execução e persistido em `config.json`.

### Como o WorkspaceRoot chega ao WorkspaceService

```
Arranque da app
    └── AppPaths.ReadWorkspaceRootFromConfig()       // leitura síncrona antes do DI
            └── new AppPaths(workspaceRoot)              // IAppPaths construído com o valor
                    └── services.AddSingleton<IAppPaths>(appPaths)
                            └── WorkspaceService(IAppPaths appPaths)  // recebe via DI
```

### Primeira execução sem workspace configurado

```
IsInitializedAsync() == false
    └── UI: FirstRunSetupView
            └── Utilizador escolhe pasta raiz
            └── IWorkspaceService.EnsureWorkspaceRootAsync(path)  // cria pasta + _archive
            └── ISettingsService.SaveAsync(settings com WorkspaceRoot = path)
            └── App reinicia para reconstruir IAppPaths com o novo workspace
```

Ver SETTINGS.md para o detalhe completo do fluxo de bootstrap e implementação de `ISettingsService`.

---

## 10. Nota crítica sobre UI e passwords

A UI deve:
1. abrir um `ReAuthenticateDialog`
2. recolher a password
3. chamar `IReAuthenticationService.ConfirmPasswordAsync(...)`
4. se sucesso, só então chamar o método destrutivo do serviço

### Melhor abordagem

Em vez de passar plaintext password até ao método destrutivo, criar um token curto em memória:

```csharp
ICriticalActionTokenService
```

Exemplo:
- `CreateToken("repository.delete", userId, 30 seconds)`
- o serviço recebe esse token e valida antes de apagar

Isto é o padrão usado na app.

---

## 11. IAppPaths

```csharp
// src/DevTracker.Application/Abstractions/IO/IAppPaths.cs
namespace DevTracker.Application.Abstractions.IO;

public interface IAppPaths
{
    string AppDataRoot { get; }
    string DatabasePath { get; }
    string LogsPath { get; }
    string ConfigPath { get; }
    string WorkspaceRoot { get; }
    bool IsWorkspaceConfigured { get; }
}
```

### Implementação sugerida

```csharp
// src/DevTracker.Infrastructure/IO/AppPaths.cs
namespace DevTracker.Infrastructure.IO;

public sealed class AppPaths : IAppPaths
{
    public string AppDataRoot { get; }
    public string DatabasePath { get; }
    public string LogsPath { get; }
    public string ConfigPath { get; }
    public string WorkspaceRoot { get; }
    public bool IsWorkspaceConfigured => !string.IsNullOrWhiteSpace(WorkspaceRoot);

    public AppPaths(string workspaceRoot)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AppDataRoot = Path.Combine(local, "DevTracker");
        DatabasePath = Path.Combine(AppDataRoot, "data.db");
        LogsPath = Path.Combine(AppDataRoot, "logs");
        ConfigPath = Path.Combine(AppDataRoot, "config.json");
        WorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot)
            ? string.Empty
            : Path.GetFullPath(workspaceRoot);
    }
}
```

---

## 12. Operações recomendadas por camadas

### Application layer

- validar intenção de negócio
- verificar permissões
- chamar `IWorkspaceService`
- persistir alterações na BD
- gerar audit logs

### Infrastructure layer

- executar IO real com `Directory`, `Path`, `File`
- normalizar paths
- garantir regras de segurança do filesystem

### UI layer

- nunca fazer IO de negócio
- nunca construir paths manuais fora de apresentação visual

---

## 13. Estratégia de delete

### Projeto

Delete lógico:
- `Project.IsDeleted = true`
- `DeletedAt = utcNow`

Delete físico:
- mover pasta do projeto para `_archive`

### Repositório

Delete lógico:
- `Repository.IsDeleted = true`
- `DeletedAt = utcNow`

Delete físico:
- mover pasta para `_archive`

### Vantagem

- reduz risco de perda acidental
- permite restauro manual
- mantém coerência entre domínio e filesystem

---

## 14. Riscos e mitigação

### Risco: path traversal
Mitigação:
- `Path.GetFullPath`
- check de prefixo contra `WorkspaceRoot`
- nomes sanitizados

### Risco: delete errado
Mitigação:
- `_archive` em vez de delete imediato
- re-autenticação
- audit log

### Risco: colisão de nomes
Mitigação:
- rejeitar se pasta já existir
- ou futura estratégia de `name (1)`

### Risco: attach mover pasta externa importante
Mitigação:
- UI deve mostrar confirmação clara com origem e destino antes do move
- `AttachExistingFolderAsync` já suporta cross-volume via Copy+Delete
- Futura opção: modo de só cópia (sem remover a origem)

---

## 15. Dependency Injection

```csharp
// src/DevTracker.Infrastructure/DependencyInjection.cs
using DevTracker.Application.Abstractions.IO;
using DevTracker.Infrastructure.IO;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Infrastructure;

public static partial class DependencyInjection
{
    public static IServiceCollection AddFileSystem(this IServiceCollection services, string workspaceRoot)
    {
        services.AddSingleton<IAppPaths>(_ => new AppPaths(workspaceRoot));
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
```

---

## 16. Regras finais

1. Todo o acesso ao filesystem passa por `IWorkspaceService`.
2. Nada sai fora do `WorkspaceRoot`.
3. Names são sempre sanitizados.
4. Deletes físicos vão primeiro para `_archive`.
5. Operações destrutivas exigem re-autenticação.
6. Todas as operações relevantes deixam rasto em auditoria.
7. A UI nunca é autoridade sobre paths nem permissões.
8. `AttachExistingFolderAsync` suporta cross-volume via Copy+Delete automático.
9. `ListEntriesAsync` e `CopyDirectoryContentsAsync` são as abstrações de IO read-only/cópia.
10. O WorkspaceRoot é lido de `config.json` no arranque via `AppPaths.ReadWorkspaceRootFromConfig()` — ver SETTINGS.md.

