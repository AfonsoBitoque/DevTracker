using DevTracker.Application.Common;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Core.Enums;

namespace DevTracker.Application.Abstractions.Services;

public interface IWorkItemService
{
    Task<Result<IReadOnlyList<WorkItemSummaryDto>>> GetByProjectAsync(
        Guid              projectId,
        WorkItemStatus?   filterStatus = null,
        WorkItemType?     filterType   = null,
        CancellationToken ct           = default);

    Task<Result<WorkItemDetailDto>> GetByIdAsync(Guid workItemId, CancellationToken ct = default);
    Task<Result<Guid>>              CreateAsync(CreateWorkItemRequest request, CancellationToken ct = default);
    Task<Result>                    UpdateAsync(UpdateWorkItemRequest request, CancellationToken ct = default);
    Task<Result>                    ChangeStatusAsync(ChangeWorkItemStatusRequest request, CancellationToken ct = default);
    Task<Result>                    AddCommentAsync(AddCommentRequest request, CancellationToken ct = default);
    Task<Result>                    DeleteAsync(Guid workItemId, CancellationToken ct = default);
    Task<Result>                    UpdateLabelsAsync(Guid workItemId, IEnumerable<string> labelNames, CancellationToken ct = default);
}
