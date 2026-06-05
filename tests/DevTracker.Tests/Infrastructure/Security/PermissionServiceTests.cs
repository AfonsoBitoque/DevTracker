using DevTracker.Application.Common;
using DevTracker.Application.Security;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Security;
using DevTracker.Tests.Helpers;
using FluentAssertions;

namespace DevTracker.Tests.Infrastructure.Security;

/// <summary>
/// Testa a matriz de permissões para todos os roles.
/// Usa SQLite in-memory para garantir FK constraints e QueryFilters.
/// </summary>
public sealed class PermissionServiceTests
{
    // ── CanPerformAsync ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(UserRole.Owner,      Permissions.ProjectDelete, true)]
    [InlineData(UserRole.Admin,      Permissions.ProjectDelete, false)]
    [InlineData(UserRole.Maintainer, Permissions.ProjectDelete, false)]
    [InlineData(UserRole.Reader,     Permissions.ProjectDelete, false)]
    [InlineData(UserRole.Owner,      Permissions.ProjectCreate, true)]
    [InlineData(UserRole.Admin,      Permissions.ProjectCreate, true)]
    [InlineData(UserRole.Maintainer, Permissions.ProjectCreate, false)]
    [InlineData(UserRole.Reader,     Permissions.ProjectRead,   true)]
    [InlineData(UserRole.Reader,     Permissions.WorkItemComment, true)]
    [InlineData(UserRole.Reader,     Permissions.WorkItemCreate,  false)]
    public async Task CanPerformAsync_ForRole_ReturnsExpected(UserRole role, string permission, bool expected)
    {
        // Arrange
        using var db   = TestDbContextFactory.Create();
        var user       = new UserBuilder().WithRole(role).Build();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new PermissionService(db);

        // Act
        var result = await service.CanPerformAsync(user.Id, permission);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task CanPerformAsync_InactiveUser_ReturnsFalse()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user     = new UserBuilder().WithRole(UserRole.Owner).Inactive().Build();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new PermissionService(db);

        // Act
        var result = await service.CanPerformAsync(user.Id, Permissions.ProjectCreate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanPerformAsync_NonExistentUser_ReturnsFalse()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service  = new PermissionService(db);

        // Act
        var result = await service.CanPerformAsync(Guid.NewGuid(), Permissions.ProjectCreate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCanPerformAsync_UserWithoutPermission_ThrowsAuthorizationException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user     = new UserBuilder().WithRole(UserRole.Reader).Build();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new PermissionService(db);

        // Act & Assert
        await service.Invoking(s => s.EnsureCanPerformAsync(user.Id, Permissions.ProjectDelete))
            .Should().ThrowAsync<AuthorizationException>();
    }
}
