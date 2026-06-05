using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Implementa IAuthService e IReAuthenticationService (mesma classe — mesma responsabilidade).
/// Usa Argon2id para hashing. Sessão gerida pelo ISessionService (em memória).
/// SetupFirstOwnerAsync só funciona uma vez. Audit obrigatório em Login/Logout/UserCreated/PasswordChanged.
/// </summary>
public sealed class AuthService(
    AppDbContext       db,
    IPasswordHasher    passwordHasher,
    ISessionService    sessionService,
    IAuditService      auditService) : IAuthService, IReAuthenticationService
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);

    public async Task<AuthResult> SetupFirstOwnerAsync(string username, string password, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
            return AuthResult.Fail("Users already exist in the system.");

        var pwd  = passwordHasher.Hash(password);
        var user = new User
        {
            Username     = username.Trim(),
            PasswordHash = pwd.Hash,
            Salt         = pwd.Salt,
            Role         = UserRole.Owner,
            IsActive     = true,
            LastLoginAt  = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var session = CreateSession(user);
        sessionService.Start(session);

        await auditService.LogAsync(user.Id, AuditAction.UserCreated, nameof(User), user.Id,
            null, $"{{\"username\":\"{user.Username}\",\"role\":\"Owner\"}}", ct);
        await auditService.LogAsync(user.Id, AuditAction.Login, nameof(User), user.Id,
            null, "Bootstrap first owner", ct);

        return AuthResult.Success(ToSessionInfo(session));
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username && u.IsActive, ct);
        if (user is null || !passwordHasher.Verify(password, user.PasswordHash, user.Salt))
            return AuthResult.Fail("Invalid credentials.");

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var session = CreateSession(user);
        sessionService.Start(session);

        await auditService.LogAsync(user.Id, AuditAction.Login, nameof(User), user.Id, null, null, ct);

        return AuthResult.Success(ToSessionInfo(session));
    }

    public async Task<Result> LogoutAsync(CancellationToken ct = default)
    {
        var userId = sessionService.Current?.UserId;
        sessionService.End();

        if (userId.HasValue)
            await auditService.LogAsync(userId, AuditAction.Logout, nameof(User), userId, null, null, ct);

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var userId = sessionService.Current?.UserId;
        if (!userId.HasValue) return Result.Fail("Username não autenticado.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail("Username não encontrado.");

        if (!passwordHasher.Verify(currentPassword, user.PasswordHash, user.Salt))
            return Result.Fail("Password atual incorreta.");

        var pwd          = passwordHasher.Hash(newPassword);
        user.PasswordHash = pwd.Hash;
        user.Salt         = pwd.Salt;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(userId, AuditAction.PasswordChanged, nameof(User), userId.Value, null, null, ct);

        return Result.Success();
    }

    public Task<bool> AnyUsersExistAsync(CancellationToken ct = default)
        => db.Users.AnyAsync(ct);

    public async Task<Result> ConfirmPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);

        return user is not null && passwordHasher.Verify(password, user.PasswordHash, user.Salt)
            ? Result.Success()
            : Result.Fail("Incorrect password.");
    }

    private static UserSession CreateSession(User user) => new(
        Token:           Guid.NewGuid(),
        UserId:          user.Id,
        Username:        user.Username,
        Role:            user.Role,
        StartedAtUtc:    DateTime.UtcNow,
        ExpiresAtUtc:    DateTime.UtcNow.Add(SessionDuration),
        LastActivityUtc: DateTime.UtcNow);

    private static SessionInfo ToSessionInfo(UserSession s) =>
        new(s.UserId, s.Username, s.Role.ToString(), IsLocked: false);
}
