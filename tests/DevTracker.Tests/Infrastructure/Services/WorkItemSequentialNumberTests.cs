using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Services;
using DevTracker.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DevTracker.Tests.Infrastructure.Services;

/// <summary>
/// Verifica que Work Items recebem números sequenciais por projeto,
/// incluindo quando há soft-deleted items (para nunca reutilizar números).
/// </summary>
public sealed class WorkItemSequentialNumberTests
{
    private static WorkItemService CreateService(
        DevTracker.Infrastructure.Persistence.AppDbContext db,
        Guid userId)
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId);
        currentUser.IsAuthenticated.Returns(true);

        var permissionSvc = Substitute.For<IPermissionService>();
        permissionSvc.EnsureCanPerformAsync(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var auditSvc = Substitute.For<IAuditService>();

        return new WorkItemService(db, currentUser, permissionSvc, auditSvc);
    }

    [Fact]
    public async Task CreateAsync_FirstItemInProject_HasNumberOne()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var owner     = new UserBuilder().WithRole(UserRole.Owner).Build();
        var project   = new ProjectBuilder().Build();
        db.Users.Add(owner);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var service = CreateService(db, owner.Id);
        var request = new CreateWorkItemRequest(project.Id, "Task #1", null, WorkItemType.Task, WorkItemPriority.Medium, WorkItemDifficulty.Medium, WorkItemEstimatedTime.OneDay, null, null);

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        var item = db.WorkItems.Single(w => w.Id == result.Value);
        item.Number.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_MultipleItems_NumbersAreSequential()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var owner     = new UserBuilder().WithRole(UserRole.Owner).Build();
        var project   = new ProjectBuilder().Build();
        db.Users.Add(owner);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var service = CreateService(db, owner.Id);
        var req     = new CreateWorkItemRequest(project.Id, "Item", null, WorkItemType.Task, WorkItemPriority.Low, WorkItemDifficulty.Medium, WorkItemEstimatedTime.OneDay, null, null);

        // Act
        var id1 = await service.CreateAsync(req with { Title = "Item 1" });
        var id2 = await service.CreateAsync(req with { Title = "Item 2" });

        // Assert
        var item1 = db.WorkItems.Single(w => w.Id == id1.Value);
        var item2 = db.WorkItems.Single(w => w.Id == id2.Value);
        item1.Number.Should().Be(1);
        item2.Number.Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_AfterSoftDelete_NumberContinuesSequentially()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var owner     = new UserBuilder().WithRole(UserRole.Owner).Build();
        var project   = new ProjectBuilder().Build();
        db.Users.Add(owner);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var service = CreateService(db, owner.Id);
        var req     = new CreateWorkItemRequest(project.Id, "Item", null, WorkItemType.Task, WorkItemPriority.Low, WorkItemDifficulty.Medium, WorkItemEstimatedTime.OneDay, null, null);

        var id1 = await service.CreateAsync(req with { Title = "Item 1" });

        // Soft delete do primeiro item
        await service.DeleteAsync(id1.Value!);

        // Act — próximo item não deve reutilizar o número 1
        var id2 = await service.CreateAsync(req with { Title = "Item 2" });

        // Assert
        var item2 = db.WorkItems.IgnoreQueryFilters().Single(w => w.Id == id2.Value);
        item2.Number.Should().Be(2);
    }
}
