using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Repositories;
using DevTracker.Application.Security;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

public sealed class RepositoryService(
    AppDbContext        db,
    ICurrentUserService currentUser,
    IPermissionService  permissionService,
    IAuditService       auditService,
    IWorkspaceService   workspaceService) : IRepositoryService
{
    public async Task<Result<IReadOnlyList<RepositorySummaryDto>>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<IReadOnlyList<RepositorySummaryDto>>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.RepositoryRead, ct);

        var repos = await db.Repositories.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Select(r => new RepositorySummaryDto(r.Id, r.Name, r.RelativePath, r.Description, r.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RepositorySummaryDto>>.Success(repos);
    }

    public async Task<Result<Guid>> CreateAsync(CreateRepositoryRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<Guid>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.RepositoryCreate, ct);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == request.ProjectId, ct);
        if (project is null) return Result<Guid>.Fail("Project not found.");

        var folderResult = await workspaceService.CreateRepositoryFolderAsync(
            request.ProjectId, project.WorkspacePath, request.Name, ct);

        if (!folderResult.Succeeded) return Result<Guid>.Fail(folderResult.Error!);

        // RelativePath = caminho relativo ao WorkspacePath do projeto
        var relativePath = Path.GetRelativePath(project.WorkspacePath, folderResult.Value!);
        var repo = new Repository
        {
            ProjectId    = request.ProjectId,
            Name         = request.Name.Trim(),
            RelativePath = relativePath,
            Description  = request.Description?.Trim()
        };

        db.Repositories.Add(repo);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryCreated,
            nameof(Repository), repo.Id, null, $"{{\"name\":\"{repo.Name}\"}}", ct);

        return Result<Guid>.Success(repo.Id);
    }

    public async Task<Result<Guid>> AttachAsync(AttachRepositoryRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<Guid>.Fail("Not authenticated.");

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == request.ProjectId, ct);
        if (project is null) return Result<Guid>.Fail("Project not found.");

        var attachResult = await workspaceService.AttachExistingFolderAsync(request.ProjectId, request.SourceAbsolutePath, ct);
        if (!attachResult.Succeeded) return Result<Guid>.Fail(attachResult.Error!);

        var relativePath = Path.GetRelativePath(project.WorkspacePath, attachResult.Value!);
        var repo = new Repository
        {
            ProjectId    = request.ProjectId,
            Name         = Path.GetFileName(attachResult.Value!),
            RelativePath = relativePath,
            Description  = request.Description?.Trim()
        };

        db.Repositories.Add(repo);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(repo.Id);
    }

    public async Task<Result> RenameAsync(Guid repositoryId, string newName, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.RepositoryRename, ct);

        var repo    = await db.Repositories.Include(r => r.Project).SingleOrDefaultAsync(r => r.Id == repositoryId, ct);
        if (repo is null) return Result.Fail("Repositório não encontrado.");

        var repoAbsPath = Path.Combine(repo.Project.WorkspacePath, repo.RelativePath);
        var renameResult = await workspaceService.RenameRepositoryFolderAsync(repositoryId, repoAbsPath, newName, ct);
        if (!renameResult.Succeeded) return Result.Fail(renameResult.Error!);

        repo.Name         = newName.Trim();
        repo.RelativePath = Path.GetRelativePath(repo.Project.WorkspacePath, renameResult.Value!);

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid repositoryId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.RepositoryDelete, ct);

        var repo = await db.Repositories.Include(r => r.Project).SingleOrDefaultAsync(r => r.Id == repositoryId, ct);
        if (repo is null) return Result.Fail("Repositório não encontrado.");

        var repoAbsPath = Path.Combine(repo.Project.WorkspacePath, repo.RelativePath);
        var archiveResult = await workspaceService.ArchiveRepositoryFolderAsync(repositoryId, repoAbsPath, ct);
        if (!archiveResult.Succeeded) return Result.Fail(archiveResult.Error!);

        repo.IsDeleted = true;
        repo.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.RepositoryDeleted,
            nameof(Repository), repositoryId, repo.Name, null, ct);

        return Result.Success();
    }
}
