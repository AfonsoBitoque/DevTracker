using DevTracker.Infrastructure.Security;
using DevTracker.Infrastructure.Services;
using DevTracker.Tests.Helpers;
using FluentAssertions;
using NSubstitute;

namespace DevTracker.Tests.Infrastructure.Security;

public sealed class AuthServiceTests
{
    private static AuthService CreateService(
        DevTracker.Infrastructure.Persistence.AppDbContext db,
        DevTracker.Application.Abstractions.Security.ISessionService? session = null,
        DevTracker.Application.Abstractions.Services.IAuditService? audit = null)
    {
        var hasher    = new Argon2PasswordHasher();
        var sessionSvc = session ?? Substitute.For<DevTracker.Application.Abstractions.Security.ISessionService>();
        var auditSvc   = audit   ?? Substitute.For<DevTracker.Application.Abstractions.Services.IAuditService>();
        return new AuthService(db, hasher, sessionSvc, auditSvc);
    }

    [Fact]
    public async Task SetupFirstOwnerAsync_WhenNoUsers_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service  = CreateService(db);

        // Act
        var result = await service.SetupFirstOwnerAsync("admin", "MyP@ssword123!");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Session.Should().NotBeNull();
        db.Users.Should().HaveCount(1);
    }

    [Fact]
    public async Task SetupFirstOwnerAsync_WhenUsersExist_ReturnsFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user     = new UserBuilder().WithRole(DevTracker.Core.Enums.UserRole.Owner).Build();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.SetupFirstOwnerAsync("admin2", "MyP@ssword123!");

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsSuccess()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var service   = CreateService(db);
        await service.SetupFirstOwnerAsync("admin", "MyP@ssword123!");

        // Act
        var result = await service.LoginAsync("admin", "MyP@ssword123!");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Session.Should().NotBeNull();
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service  = CreateService(db);
        await service.SetupFirstOwnerAsync("admin", "MyP@ssword123!");

        // Act
        var result = await service.LoginAsync("admin", "WrongPassword!");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ReturnsFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service  = CreateService(db);

        // Act
        var result = await service.LoginAsync("nobody", "SomePassword123!");

        // Assert
        result.Succeeded.Should().BeFalse();
    }
}
