using DevTracker.Application.DTOs.Repositories;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Projects;

// ── Requests ────────────────────────────────────────────────────────────────

public sealed record CreateProjectRequest(
    string  Name,
    string? Description,
    string  Color,
    string  WorkspacePath,
    string? GitHubRepositoryUrl);

public sealed record UpdateProjectRequest(
    Guid    Id,
    string  Name,
    string? Description,
    string  Color,
    string? GitHubRepositoryUrl,
    string? UsedAiName = null);

public sealed record ChangeProjectStateRequest(
    Guid         ProjectId,
    ProjectState NewState,
    string?      Reason);

// ── Responses ───────────────────────────────────────────────────────────────

public sealed record ProjectSummaryDto(
    Guid         Id,
    string       Name,
    string?      Description,
    string       Color,
    ProjectState State,
    int          WorkItemCount,
    int          RepositoryCount,
    string?      GitHubRepositoryUrl,
    string?      UsedAiName,
    DateTime     CreatedAt,
    DateTime     UpdatedAt);

public sealed record ProjectDetailDto(
    Guid                             Id,
    string                           Name,
    string?                          Description,
    string                           Color,
    string                           WorkspacePath,
    ProjectState                     State,
    string?                          GitHubRepositoryUrl,
    string?                          UsedAiName,
    IReadOnlyList<RepositorySummaryDto> Repositories,
    IReadOnlyList<WorkItemSummaryDto>   WorkItems,
    DateTime                         CreatedAt,
    DateTime                         UpdatedAt);
