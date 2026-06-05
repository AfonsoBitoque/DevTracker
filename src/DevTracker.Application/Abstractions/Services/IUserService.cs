using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Users;

namespace DevTracker.Application.Abstractions.Services;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserSummaryDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<UserSummaryDto>>               GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<Result<Guid>>                         CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result>                               ChangeRoleAsync(ChangeUserRoleRequest request, CancellationToken ct = default);
    Task<Result>                               DeactivateAsync(Guid userId, CancellationToken ct = default);
    Task<Result>                               DeleteAsync(Guid userId, CancellationToken ct = default);
}
