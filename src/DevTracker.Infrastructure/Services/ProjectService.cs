using System.IO;
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Security;
using DevTracker.Application.Validators.Projects;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

/// <summary>
/// Gestão de projetos. Fluxo padrão: verificar auth → EnsureCanPerform → validar DTO → lógica de negócio.
/// Soft delete: IsDeleted=true + DeletedAt. Delete físico via IWorkspaceService (coordenado pela UI).
/// </summary>
public sealed class ProjectService(
    AppDbContext        db,
    ICurrentUserService currentUser,
    IPermissionService  permissionService,
    IAuditService       auditService,
    IWorkspaceService   workspaceService,
    IGitService         gitService,
    IGitHubService      gitHubService,
    ISecretService      secretService) : IProjectService
{
    private readonly CreateProjectRequestValidator     _createValidator     = new();
    private readonly ChangeProjectStateRequestValidator _stateValidator = new();

    public async Task<Result<IReadOnlyList<ProjectSummaryDto>>> GetAllAsync(CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<IReadOnlyList<ProjectSummaryDto>>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectRead, ct);

        var items = await db.Projects.AsNoTracking()
            .Select(p => new ProjectSummaryDto(
                p.Id, p.Name, p.Description, p.Color, p.State,
                p.WorkItems.Count(w => !w.IsDeleted),
                p.Repositories.Count(r => !r.IsDeleted),
                p.GitHubRepositoryUrl,
                p.UsedAiName,
                p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProjectSummaryDto>>.Success(items);
    }

    public async Task<Result<ProjectDetailDto>> GetByIdAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<ProjectDetailDto>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectRead, ct);

        var p = await db.Projects.AsNoTracking()
            .Include(x => x.Repositories.Where(r => !r.IsDeleted))
            .Include(x => x.WorkItems.Where(w => !w.IsDeleted))
                .ThenInclude(w => w.AssignedToUser)
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);

        if (p is null) return Result<ProjectDetailDto>.Fail("Project not found.");

        return Result<ProjectDetailDto>.Success(MapToDetail(p));
    }

    public async Task<Result<Guid>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<Guid>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectCreate, ct);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<Guid>.Fail(validation.Errors[0].ErrorMessage);

        if (await db.Projects.AnyAsync(p => p.Name == request.Name, ct))
            return Result<Guid>.Fail("A project with this name already exists.");

        // Create pasta física no workspace com o nome do projeto
        var folderResult = await workspaceService.CreateProjectFolderAsync(Guid.Empty, request.Name.Trim(), ct);
        if (!folderResult.Succeeded) return Result<Guid>.Fail(folderResult.Error!);

        string? repoUrl = request.GitHubRepositoryUrl?.Trim();

        // Create repositorio GitHub automaticamente se nao fornecido URL mas houver token
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            var tokenResult = await secretService.GetSecretAsync("GitHubToken", ct);
            if (tokenResult.Succeeded && !string.IsNullOrWhiteSpace(tokenResult.Value))
            {
                gitHubService.SetAuthToken(tokenResult.Value!);
                var createResult = await gitHubService.CreateRepositoryAsync(
                    request.Name.Trim(),
                    request.Description?.Trim(),
                    isPrivate: false,
                    ct);

                if (!createResult.Succeeded)
                {
                    // Rollback: limpar pasta fisica
                    try
                    {
                        if (Directory.Exists(folderResult.Value!))
                            Directory.Delete(folderResult.Value!, recursive: true);
                    }
                    catch { /* best effort cleanup */ }

                    return Result<Guid>.Fail($"Error creating GitHub repository: {createResult.Error}");
                }

                repoUrl = createResult.Value;
            }
        }

        var project = new Project
        {
            Name               = request.Name.Trim(),
            Description        = request.Description?.Trim(),
            Color              = request.Color,
            WorkspacePath      = folderResult.Value!,
            GitHubRepositoryUrl = repoUrl,
            State              = ProjectState.NotStarted
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        // Clone automatico se GitHub URL existir
        if (!string.IsNullOrWhiteSpace(project.GitHubRepositoryUrl))
        {
            var tokenResult = await secretService.GetSecretAsync("GitHubToken", ct);
            var token = tokenResult.Succeeded ? tokenResult.Value : null;

            var cloneResult = await gitService.CloneAsync(
                project.GitHubRepositoryUrl,
                project.WorkspacePath,
                token,
                ct);

            if (!cloneResult.Succeeded)
            {
                // Rollback: remover da BD
                db.Projects.Remove(project);
                await db.SaveChangesAsync(ct);

                // Rollback: limpar pasta fisica
                try
                {
                    if (Directory.Exists(project.WorkspacePath))
                        Directory.Delete(project.WorkspacePath, recursive: true);
                }
                catch { /* best effort cleanup */ }

                return Result<Guid>.Fail($"Clone failed: {cloneResult.Error}");
            }
        }

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectCreated,
            nameof(Project), project.Id, null, $"{{\"name\":\"{project.Name}\"}}", ct);

        return Result<Guid>.Success(project.Id);
    }

    public async Task<Result> UpdateAsync(UpdateProjectRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectUpdate, ct);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == request.Id, ct);
        if (project is null) return Result.Fail("Project not found.");

        if (await db.Projects.AnyAsync(p => p.Name == request.Name && p.Id != request.Id, ct))
            return Result.Fail("A project with this name already exists.");

        var old = $"{{\"name\":\"{project.Name}\"}}";
        project.Name               = request.Name.Trim();
        project.Description        = request.Description?.Trim();
        project.Color              = request.Color;
        project.GitHubRepositoryUrl = request.GitHubRepositoryUrl?.Trim();
        project.UsedAiName         = request.UsedAiName?.Trim();

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectUpdated,
            nameof(Project), project.Id, old, $"{{\"name\":\"{project.Name}\"}}", ct);

        return Result.Success();
    }

    public async Task<Result> ChangeStateAsync(ChangeProjectStateRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectChangeState, ct);

        var validation = await _stateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result.Fail(validation.Errors[0].ErrorMessage);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == request.ProjectId, ct);
        if (project is null) return Result.Fail("Project not found.");

        var transition = new StateTransition
        {
            ProjectId       = project.Id,
            FromState       = project.State,
            ToState         = request.NewState,
            Reason          = request.Reason,
            ChangedByUserId = currentUser.UserId.Value
        };

        project.State = request.NewState;
        db.StateTransitions.Add(transition);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectStateChanged,
            nameof(Project), project.Id,
            transition.FromState.ToString(), transition.ToState.ToString(), ct);

        return Result.Success();
    }

    public async Task<Result> ArchiveAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectArchive, ct);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Result.Fail("Project not found.");

        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectDelete, ct);

        var project = await db.Projects.IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Result.Fail("Project not found.");

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.ProjectDeleted,
            nameof(Project), projectId, project.Name, null, ct);

        return Result.Success();
    }

    // ── Mappers ──────────────────────────────────────────────────────────────

    private static ProjectDetailDto MapToDetail(Project p) => new(
        p.Id, p.Name, p.Description, p.Color, p.WorkspacePath, p.State, p.GitHubRepositoryUrl,
        p.UsedAiName,
        p.Repositories.Select(r => new Application.DTOs.Repositories.RepositorySummaryDto(
            r.Id, r.Name, r.RelativePath, r.Description, r.CreatedAt)).ToList(),
        p.WorkItems.Select(w => new Application.DTOs.WorkItems.WorkItemSummaryDto(
            w.Id, w.Number, w.Title, w.Type, w.Status, w.Priority, w.Difficulty, w.EstimatedTime,
            w.EstimatedPrompts, w.ActualPrompts, w.AiPromptUsed,
            w.AssignedToUser?.Username, w.DueDate, w.UpdatedAt, w.Description)).ToList(),
        p.CreatedAt, p.UpdatedAt);
}
