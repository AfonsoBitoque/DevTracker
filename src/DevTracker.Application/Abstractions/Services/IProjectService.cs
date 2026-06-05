using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;

namespace DevTracker.Application.Abstractions.Services;

public interface IProjectService
{
    Task<Result<IReadOnlyList<ProjectSummaryDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ProjectDetailDto>>                 GetByIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<Guid>>                             CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<Result>                                   UpdateAsync(UpdateProjectRequest request, CancellationToken ct = default);
    Task<Result>                                   ChangeStateAsync(ChangeProjectStateRequest request, CancellationToken ct = default);
    Task<Result>                                   ArchiveAsync(Guid projectId, CancellationToken ct = default);
    Task<Result>                                   DeleteAsync(Guid projectId, CancellationToken ct = default);
}
