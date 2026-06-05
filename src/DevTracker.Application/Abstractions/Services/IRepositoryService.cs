using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Repositories;

namespace DevTracker.Application.Abstractions.Services;

public interface IRepositoryService
{
    Task<Result<IReadOnlyList<RepositorySummaryDto>>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<Guid>>                                CreateAsync(CreateRepositoryRequest request, CancellationToken ct = default);
    Task<Result<Guid>>                                AttachAsync(AttachRepositoryRequest request, CancellationToken ct = default);
    Task<Result>                                      RenameAsync(Guid repositoryId, string newName, CancellationToken ct = default);
    Task<Result>                                      DeleteAsync(Guid repositoryId, CancellationToken ct = default);
}
