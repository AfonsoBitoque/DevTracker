using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Application.Security;
using DevTracker.Application.Validators.WorkItems;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

/// <summary>
/// Gestão de work items. Number é gerado como MAX(Number)+1 por projeto com IgnoreQueryFilters
/// para contabilizar soft-deleted e evitar colisões.
/// </summary>
public sealed class WorkItemService(
    AppDbContext        db,
    ICurrentUserService currentUser,
    IPermissionService  permissionService,
    IAuditService       auditService) : IWorkItemService
{
    private readonly CreateWorkItemRequestValidator _createValidator = new();
    private readonly AddCommentRequestValidator     _commentValidator = new();

    public async Task<Result<IReadOnlyList<WorkItemSummaryDto>>> GetByProjectAsync(
        Guid projectId, WorkItemStatus? filterStatus = null, WorkItemType? filterType = null, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<IReadOnlyList<WorkItemSummaryDto>>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemRead, ct);

        var query = db.WorkItems.AsNoTracking()
            .Include(w => w.AssignedToUser)
            .Where(w => w.ProjectId == projectId);

        if (filterStatus.HasValue) query = query.Where(w => w.Status == filterStatus.Value);
        if (filterType.HasValue)   query = query.Where(w => w.Type   == filterType.Value);

        var items = await query.OrderBy(w => w.Number)
            .Select(w => new WorkItemSummaryDto(
                w.Id, w.Number, w.Title, w.Type, w.Status, w.Priority, w.Difficulty, w.EstimatedTime,
                w.EstimatedPrompts, w.ActualPrompts, w.AiPromptUsed,
                w.AssignedToUser != null ? w.AssignedToUser.Username : null,
                w.DueDate, w.UpdatedAt, w.Description))
            .ToListAsync(ct);

        return Result<IReadOnlyList<WorkItemSummaryDto>>.Success(items);
    }

    public async Task<Result<WorkItemDetailDto>> GetByIdAsync(Guid workItemId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<WorkItemDetailDto>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemRead, ct);

        var w = await db.WorkItems.AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.CreatedByUser)
            .Include(x => x.AssignedToUser)
            .Include(x => x.Comments.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.AuthorUser)
            .Include(x => x.Labels)
                .ThenInclude(l => l.Label)
            .SingleOrDefaultAsync(x => x.Id == workItemId, ct);

        if (w is null) return Result<WorkItemDetailDto>.Fail("Work item not found.");

        return Result<WorkItemDetailDto>.Success(new WorkItemDetailDto(
            w.Id, w.Number, w.Title, w.Description, w.Type, w.Status, w.Priority, w.Difficulty, w.EstimatedTime,
            w.EstimatedPrompts, w.ActualPrompts,
            w.ProjectId, w.Project.Name, w.CreatedByUser.Username,
            w.AssignedToUser?.Username,
            w.Comments.Select(c => new CommentDto(c.Id, c.AuthorUser.Username, c.Body, c.CreatedAt, c.EditedAt)).ToList(),
            w.Labels.Select(l => l.Label.Name).ToList(),
            w.DueDate, w.CreatedAt, w.UpdatedAt,
            w.MomentumNote));
    }

    public async Task<Result<Guid>> CreateAsync(CreateWorkItemRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<Guid>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemCreate, ct);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<Guid>.Fail(validation.Errors[0].ErrorMessage);

        // Number sequencial por projeto — inclui soft-deleted para nunca reutilizar números
        var nextNumber = (await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ProjectId == request.ProjectId)
            .MaxAsync(w => (int?)w.Number, ct) ?? 0) + 1;

        var item = new WorkItem
        {
            ProjectId        = request.ProjectId,
            Number           = nextNumber,
            Title            = request.Title.Trim(),
            Description      = request.Description?.Trim(),
            Type             = request.Type,
            Status           = WorkItemStatus.Backlog,
            Priority         = request.Priority,
            Difficulty       = request.Difficulty,
            EstimatedTime    = request.EstimatedTime,
            CreatedByUserId  = currentUser.UserId.Value,
            AssignedToUserId = request.AssignedToUserId,
            DueDate          = request.DueDate,
            EstimatedPrompts = request.EstimatedPrompts
        };

        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.WorkItemCreated,
            nameof(WorkItem), item.Id, null, $"{{\"number\":{item.Number},\"title\":\"{item.Title}\"}}", ct);

        return Result<Guid>.Success(item.Id);
    }

    public async Task<Result> UpdateAsync(UpdateWorkItemRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemUpdate, ct);

        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Id == request.Id, ct);
        if (item is null) return Result.Fail("Work item not found.");

        item.Title            = request.Title.Trim();
        item.Description      = request.Description?.Trim();
        item.Type             = request.Type;
        item.Priority         = request.Priority;
        item.Difficulty       = request.Difficulty;
        item.EstimatedTime    = request.EstimatedTime;
        item.AssignedToUserId = request.AssignedToUserId;
        item.DueDate          = request.DueDate;
        item.EstimatedPrompts = request.EstimatedPrompts;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.WorkItemUpdated,
            nameof(WorkItem), item.Id, null, null, ct);

        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(ChangeWorkItemStatusRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemChangeStatus, ct);

        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Id == request.WorkItemId, ct);
        if (item is null) return Result.Fail("Work item not found.");

        var old    = item.Status.ToString();
        item.Status = request.NewStatus;

        if (request.NewStatus == WorkItemStatus.Done && !string.IsNullOrWhiteSpace(request.AiPromptUsed))
            item.AiPromptUsed = request.AiPromptUsed.Trim();

        if (request.NewStatus == WorkItemStatus.Done && request.ActualPrompts > 0)
            item.ActualPrompts = request.ActualPrompts;

        if (request.NewStatus is WorkItemStatus.Todo or WorkItemStatus.Backlog)
            item.MomentumNote = request.MomentumNote?.Trim();

        if (request.NewStatus == WorkItemStatus.Done)
            item.MomentumNote = null;

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.WorkItemStatusChanged,
            nameof(WorkItem), item.Id, old, request.NewStatus.ToString(), ct);

        return Result.Success();
    }

    public async Task<Result> AddCommentAsync(AddCommentRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemComment, ct);

        var validation = await _commentValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result.Fail(validation.Errors[0].ErrorMessage);

        db.Comments.Add(new Comment
        {
            WorkItemId   = request.WorkItemId,
            AuthorUserId = currentUser.UserId.Value,
            Body         = request.Body.Trim()
        });

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid workItemId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemDelete, ct);

        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Id == workItemId, ct);
        if (item is null) return Result.Fail("Work item not found.");

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.WorkItemDeleted,
            nameof(WorkItem), workItemId, item.Title, null, ct);

        return Result.Success();
    }

    public async Task<Result> UpdateLabelsAsync(Guid workItemId, IEnumerable<string> labelNames, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemUpdate, ct);

        var workItem = await db.WorkItems
            .Include(w => w.Labels)
            .ThenInclude(l => l.Label)
            .SingleOrDefaultAsync(w => w.Id == workItemId, ct);

        if (workItem is null) return Result.Fail("Work item not found.");

        // Remover associações antigas
        db.WorkItemLabels.RemoveRange(workItem.Labels);

        var names = labelNames.Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (names.Count != 0)
        {
            // Procurar labels existentes
            var existingLabels = await db.Labels
                .Where(l => names.Contains(l.Name))
                .ToListAsync(ct);

            // Create labels novas
            var existingNames = existingLabels.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newLabels = names
                .Where(n => !existingNames.Contains(n))
                .Select(n => new Label { Name = n, Color = "#6366F1" })
                .ToList();

            if (newLabels.Count != 0)
            {
                db.Labels.AddRange(newLabels);
                await db.SaveChangesAsync(ct);
                existingLabels.AddRange(newLabels);
            }

            // Create associações
            var labelMap = existingLabels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                if (labelMap.TryGetValue(name, out var label))
                {
                    db.WorkItemLabels.Add(new WorkItemLabel { WorkItemId = workItemId, LabelId = label.Id });
                }
            }
        }

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.WorkItemUpdated,
            nameof(WorkItem), workItemId, null, $"{{\"labels\":[{string.Join(",", names.Select(n => $"\"{n}\""))}]}}", ct);

        return Result.Success();
    }
}
