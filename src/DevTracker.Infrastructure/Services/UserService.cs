using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Users;
using DevTracker.Application.Security;
using DevTracker.Application.Validators.Users;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

/// <summary>
/// Gestão de utilizadores. Protege sempre o último Owner ativo do sistema.
/// Admin não pode promover ninguém a Owner — só o Owner pode fazer isso.
/// </summary>
public sealed class UserService(
    AppDbContext        db,
    ICurrentUserService currentUser,
    IPermissionService  permissionService,
    IPasswordHasher     passwordHasher,
    IAuditService       auditService) : IUserService
{
    private readonly CreateUserRequestValidator _createValidator = new();

    public async Task<Result<IReadOnlyList<UserSummaryDto>>> GetAllAsync(CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<IReadOnlyList<UserSummaryDto>>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.UserRead, ct);

        var users = await db.Users.AsNoTracking()
            .Select(u => new UserSummaryDto(u.Id, u.Username, u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<UserSummaryDto>>.Success(users);
    }

    public async Task<Result<UserSummaryDto>> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<UserSummaryDto>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.UserRead, ct);

        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        return u is null
            ? Result<UserSummaryDto>.Fail("Username não encontrado.")
            : Result<UserSummaryDto>.Success(new UserSummaryDto(u.Id, u.Username, u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt));
    }

    public async Task<Result<Guid>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<Guid>.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.UserCreate, ct);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<Guid>.Fail(validation.Errors[0].ErrorMessage);

        if (await db.Users.AnyAsync(u => u.Username == request.Username, ct))
            return Result<Guid>.Fail("Já existe um utilizador com esse username.");

        var pwd  = passwordHasher.Hash(request.Password);
        var user = new User
        {
            Username     = request.Username.Trim(),
            PasswordHash = pwd.Hash,
            Salt         = pwd.Salt,
            Role         = request.Role,
            IsActive     = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.UserCreated,
            nameof(User), user.Id, null, $"{{\"username\":\"{user.Username}\",\"role\":\"{user.Role}\"}}", ct);

        return Result<Guid>.Success(user.Id);
    }

    public async Task<Result> ChangeRoleAsync(ChangeUserRoleRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.UserChangeRole, ct);

        // Admin não pode promover ninguém a Owner
        if (request.NewRole == UserRole.Owner && currentUser.Role != UserRole.Owner)
            return Result.Fail("Apenas um Owner pode promover outro utilizador a Owner.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.TargetUserId, ct);
        if (user is null) return Result.Fail("Username não encontrado.");

        if (user.Role == UserRole.Owner && request.NewRole != UserRole.Owner)
        {
            if (!await HasOtherActiveOwnerAsync(request.TargetUserId, ct))
                return Result.Fail("Não é possível alterar a role do último Owner ativo.");
        }

        var old   = user.Role.ToString();
        user.Role = request.NewRole;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.UserUpdated,
            nameof(User), user.Id, old, request.NewRole.ToString(), ct);

        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(Guid userId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.UserUpdate, ct);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail("Username não encontrado.");

        if (user.Role == UserRole.Owner && !await HasOtherActiveOwnerAsync(userId, ct))
            return Result.Fail("Não é possível desativar o último Owner ativo.");

        user.IsActive = false;
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(currentUser.UserId.Value, AuditAction.UserDeactivated,
            nameof(User), userId, null, null, ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result.Fail("Not authenticated.");
        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.UserDelete, ct);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail("Username não encontrado.");

        if (user.Role == UserRole.Owner && !await HasOtherActiveOwnerAsync(userId, ct))
            return Result.Fail("Não é possível apagar o último Owner ativo.");

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>Verifica se existe outro Owner ativo além do utilizador indicado.</summary>
    private Task<bool> HasOtherActiveOwnerAsync(Guid excludeUserId, CancellationToken ct)
        => db.Users.AnyAsync(u => u.Role == UserRole.Owner && u.IsActive && u.Id != excludeUserId, ct);
}
