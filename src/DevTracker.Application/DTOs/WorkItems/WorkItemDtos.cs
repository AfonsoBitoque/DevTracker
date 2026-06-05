using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.WorkItems;

// ── Requests ────────────────────────────────────────────────────────────────

public sealed record CreateWorkItemRequest(
    Guid                  ProjectId,
    string                Title,
    string?               Description,
    WorkItemType          Type,
    WorkItemPriority      Priority,
    WorkItemDifficulty    Difficulty,
    WorkItemEstimatedTime EstimatedTime,
    Guid?                 AssignedToUserId,
    DateTime?             DueDate,
    int                   EstimatedPrompts = 0);

public sealed record UpdateWorkItemRequest(
    Guid                  Id,
    string                Title,
    string?               Description,
    WorkItemType          Type,
    WorkItemPriority      Priority,
    WorkItemDifficulty    Difficulty,
    WorkItemEstimatedTime EstimatedTime,
    Guid?                 AssignedToUserId,
    DateTime?             DueDate,
    int                   EstimatedPrompts = 0);

public sealed record ChangeWorkItemStatusRequest(
    Guid           WorkItemId,
    WorkItemStatus NewStatus,
    string?        AiPromptUsed = null,
    string?        MomentumNote = null,
    int            ActualPrompts = 0);

public sealed record AddCommentRequest(
    Guid   WorkItemId,
    string Body);

// ── Responses ───────────────────────────────────────────────────────────────

public sealed record WorkItemSummaryDto(
    Guid                  Id,
    int                   Number,
    string                Title,
    WorkItemType          Type,
    WorkItemStatus        Status,
    WorkItemPriority      Priority,
    WorkItemDifficulty    Difficulty,
    WorkItemEstimatedTime EstimatedTime,
    int                   EstimatedPrompts,
    int                   ActualPrompts,
    string?               AiPromptUsed,
    string?               AssignedToUsername,
    DateTime?             DueDate,
    DateTime              UpdatedAt,
    string?               Description = null);

public sealed record WorkItemDetailDto(
    Guid                      Id,
    int                       Number,
    string                    Title,
    string?                   Description,
    WorkItemType              Type,
    WorkItemStatus            Status,
    WorkItemPriority          Priority,
    WorkItemDifficulty        Difficulty,
    WorkItemEstimatedTime     EstimatedTime,
    int                       EstimatedPrompts,
    int                       ActualPrompts,
    Guid                      ProjectId,
    string                    ProjectName,
    string                    CreatedByUsername,
    string?                   AssignedToUsername,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<string>     Labels,
    DateTime?                 DueDate,
    DateTime                  CreatedAt,
    DateTime                  UpdatedAt,
    string?                   MomentumNote);

public sealed record CommentDto(
    Guid      Id,
    string    AuthorUsername,
    string    Body,
    DateTime  CreatedAt,
    DateTime? EditedAt);
