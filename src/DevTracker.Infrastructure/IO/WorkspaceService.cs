using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Core.Enums;

namespace DevTracker.Infrastructure.IO;

/// <summary>
/// Ponto único de acesso ao filesystem do workspace. Garante que toda a IO fica dentro do WorkspaceRoot.
/// Operações destrutivas (Archive) devem ser precedidas de re-autenticação coordenada pela UI.
/// </summary>
public sealed class WorkspaceService(
    IPermissionService  permissionService,
    ICurrentUserService currentUser,
    IAuditService       auditService,
    IAppPaths           appPaths) : IWorkspaceService
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
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Username não autenticado.");

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
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Username não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.ProjectRename, ct);

        return await RenameFolderAsync(currentPath, WorkspaceRoot, newName,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectUpdated, "ProjectFolder", projectId, currentPath, target, ct));
    }

    public async Task<Result> ArchiveProjectFolderAsync(Guid projectId, string currentPath, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Username não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.ProjectDelete, ct);

        return await ArchiveFolderAsync(currentPath,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectDeleted, "ProjectFolder", projectId, currentPath, target, ct));
    }

    public async Task<Result<string>> CreateRepositoryFolderAsync(Guid projectId, string projectPath, string repoName, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Username não autenticado.");
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
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Username não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.RepositoryRename, ct);

        var source = NormalizeAbsolutePath(repoPath);
        var parent = Directory.GetParent(source)?.FullName ?? string.Empty;
        return await RenameFolderAsync(repoPath, parent, newName,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryRenamed, "RepositoryFolder", repositoryId, repoPath, target, ct));
    }

    public async Task<Result> ArchiveRepositoryFolderAsync(Guid repositoryId, string repoPath, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Username não autenticado.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Application.Security.Permissions.RepositoryDelete, ct);

        return await ArchiveFolderAsync(repoPath,
            target => auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryDeleted, "RepositoryFolder", repositoryId, repoPath, target, ct));
    }

    public async Task<Result<string>> AttachExistingFolderAsync(Guid projectId, string sourcePath, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<string>.Fail("Username não autenticado.");
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
            CopyDirectoryRecursive(source, target);
            Directory.Delete(source, recursive: true);
        }

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryCreated, "AttachedFolder", projectId, source, target, ct);
        return Result<string>.Success(target);
    }

    public bool IsInsideWorkspace(string absolutePath)
    {
        var full = NormalizeAbsolutePath(absolutePath);
        var root = NormalizeAbsolutePath(WorkspaceRoot);
        return full.StartsWith(root, StringComparison.Ordinal);
    }

    public string NormalizeAbsolutePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public Task<Result<IReadOnlyList<WorkspaceEntry>>> ListEntriesAsync(string absolutePath, CancellationToken ct = default)
    {
        var path = NormalizeAbsolutePath(absolutePath);
        if (!IsInsideWorkspace(path) || !Directory.Exists(path))
            return Task.FromResult(Result<IReadOnlyList<WorkspaceEntry>>.Success(Array.Empty<WorkspaceEntry>()));

        var entries = new List<WorkspaceEntry>();

        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
        {
            try
            {
                entries.Add(new WorkspaceEntry(Path.GetFileName(dir), dir, true));
            }
            catch { /* Ignorar entradas que não podem ser lidas */ }
        }

        foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
        {
            try
            {
                entries.Add(new WorkspaceEntry(Path.GetFileName(file), file, false));
            }
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
