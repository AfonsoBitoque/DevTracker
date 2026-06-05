using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Users;

// ── Requests ────────────────────────────────────────────────────────────────

public sealed record CreateUserRequest(
    string   Username,
    string   Password,
    string   ConfirmPassword,
    UserRole Role);

public sealed record ChangeUserRoleRequest(
    Guid     TargetUserId,
    UserRole NewRole);

// ── Responses ───────────────────────────────────────────────────────────────

public sealed record UserSummaryDto(
    Guid      Id,
    string    Username,
    UserRole  Role,
    bool      IsActive,
    DateTime? LastLoginAt,
    DateTime  CreatedAt);
