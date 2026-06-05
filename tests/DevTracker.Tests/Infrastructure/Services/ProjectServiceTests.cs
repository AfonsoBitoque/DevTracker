using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Security;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using DevTracker.Infrastructure.Services;
using DevTracker.Tests.Helpers;
using FluentAssertions;
using NSubstitute;

namespace DevTracker.Tests.Infrastructure.Services;

public sealed class ProjectServiceTests
{
    private static ProjectService CreateService(
        AppDbContext db,
        IWorkspaceService? workspace = null,
        IGitService? git = null,
        ISecretService? secret = null,
        Guid? userId = null)
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId ?? Guid.NewGuid());
        currentUser.IsAuthenticated.Returns(true);

        var permissionSvc = Substitute.For<IPermissionService>();
        permissionSvc.EnsureCanPerformAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var auditSvc = Substitute.For<IAuditService>();

        var workspaceSvc = workspace ?? Substitute.For<IWorkspaceService>();
        var gitSvc       = git       ?? Substitute.For<IGitService>();
        var gitHubSvc    = Substitute.For<IGitHubService>();
        var secretSvc    = secret    ?? Substitute.For<ISecretService>();

        return new ProjectService(db, currentUser, permissionSvc, auditSvc, workspaceSvc, gitSvc, gitHubSvc, secretSvc);
    }

    [Fact]
    public async Task CreateAsync_WhenCloneFails_ShouldRollbackDatabaseAndFilesystemAndReturnFailure()
    {
        // Arrange
        using var db      = TestDbContextFactory.Create();
        var owner         = new UserBuilder().WithRole(UserRole.Owner).Build();
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);

        var workspaceMock = Substitute.For<IWorkspaceService>();
        workspaceMock.CreateProjectFolderAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(tempPath));

        var gitMock = Substitute.For<IGitService>();
        gitMock.CloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Erro simulado de rede/Git"));

        var secretMock = Substitute.For<ISecretService>();
        secretMock.GetSecretAsync("GitHubToken", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("fake-token"));

        var service = CreateService(db, workspaceMock, gitMock, secretMock, owner.Id);
        var request = new CreateProjectRequest(
            "TestProject",
            null,
            "#2563EB",
            string.Empty,
            "https://github.com/test/repo");

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Clone failed");

        // Rollback: o projeto não deve estar na BD
        db.Projects.Any(p => p.Name == "TestProject").Should().BeFalse();

        // Rollback: a pasta deve ter sido removida
        Directory.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WhenCloneSucceeds_ShouldPersistProjectAndReturnSuccess()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var owner     = new UserBuilder().WithRole(UserRole.Owner).Build();
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);

        var workspaceMock = Substitute.For<IWorkspaceService>();
        workspaceMock.CreateProjectFolderAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(tempPath));

        var gitMock = Substitute.For<IGitService>();
        gitMock.CloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var secretMock = Substitute.For<ISecretService>();
        secretMock.GetSecretAsync("GitHubToken", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("fake-token"));

        var service = CreateService(db, workspaceMock, gitMock, secretMock, owner.Id);
        var request = new CreateProjectRequest(
            "TestProject",
            null,
            "#2563EB",
            string.Empty,
            "https://github.com/test/repo");

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        db.Projects.Any(p => p.Name == "TestProject").Should().BeTrue();

        // Cleanup
        if (Directory.Exists(tempPath))
            Directory.Delete(tempPath, recursive: true);
    }
}
