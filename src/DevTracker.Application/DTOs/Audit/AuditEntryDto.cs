using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Audit;

public sealed record AuditEntryDto(
    Guid        Id,
    string?     Username,
    AuditAction Action,
    string      EntityType,
    Guid?       EntityId,
    string?     OldValue,
    string?     NewValue,
    DateTime    Timestamp,
    string?     MachineName);
