using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using DevTracker.Infrastructure.Services;
using DevTracker.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DevTracker.Tests.Infrastructure.Services;

public sealed class WorkItemServiceLabelTests
{
    private static WorkItemService CreateService(AppDbContext db, Guid userId)
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
    public async Task UpdateLabelsAsync_WithNewAndExistingLabels_ShouldSyncJunctionTableCorrectly()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var owner     = new UserBuilder().WithRole(UserRole.Owner).Build();
        var project   = new ProjectBuilder().Build();
        db.Users.Add(owner);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Criar um Work Item
        var service = CreateService(db, owner.Id);
        var createResult = await service.CreateAsync(
            new CreateWorkItemRequest(project.Id, "Tarefa de teste", null, WorkItemType.Task, WorkItemPriority.Medium, WorkItemDifficulty.Medium, WorkItemEstimatedTime.OneDay, null, null));
        createResult.Succeeded.Should().BeTrue();
        var workItemId = createResult.Value;

        // Criar uma etiqueta pré-existente
        var existingLabel = new Label { Name = "Backend", Color = "#16A34A" };
        db.Labels.Add(existingLabel);
        await db.SaveChangesAsync();

        // Act — mistura de etiqueta existente + nova
        var updateResult = await service.UpdateLabelsAsync(workItemId, new[] { "Backend", "Frontend", "Bug" });

        // Assert
        updateResult.Succeeded.Should().BeTrue();

        // As novas etiquetas devem ter sido criadas
        var allLabels = await db.Labels.ToListAsync();
        allLabels.Should().Contain(l => l.Name == "Backend");
        allLabels.Should().Contain(l => l.Name == "Frontend");
        allLabels.Should().Contain(l => l.Name == "Bug");

        // A junction table deve conter exactamente as 3 associações
        var junction = await db.WorkItemLabels.Where(wl => wl.WorkItemId == workItemId).ToListAsync();
        junction.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateLabelsAsync_WhenReplacingLabels_ShouldClearOldAndSetNew()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var owner     = new UserBuilder().WithRole(UserRole.Owner).Build();
        var project   = new ProjectBuilder().Build();
        db.Users.Add(owner);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var service = CreateService(db, owner.Id);
        var createResult = await service.CreateAsync(
            new CreateWorkItemRequest(project.Id, "Tarefa de teste", null, WorkItemType.Task, WorkItemPriority.Medium, WorkItemDifficulty.Medium, WorkItemEstimatedTime.OneDay, null, null));
        var workItemId = createResult.Value;

        // Primeira atribuição
        await service.UpdateLabelsAsync(workItemId, new[] { "OldLabel" });
        var firstJunction = await db.WorkItemLabels.Where(wl => wl.WorkItemId == workItemId).ToListAsync();
        firstJunction.Should().HaveCount(1);

        // Act — substituir por outra etiqueta
        var updateResult = await service.UpdateLabelsAsync(workItemId, new[] { "NewLabel" });

        // Assert
        updateResult.Succeeded.Should().BeTrue();
        var secondJunction = await db.WorkItemLabels.Where(wl => wl.WorkItemId == workItemId).ToListAsync();
        secondJunction.Should().HaveCount(1);
        secondJunction.Single().Label.Name.Should().Be("NewLabel");
    }

    [Fact]
    public async Task UpdateLabelsAsync_WithEmptyList_ShouldClearAllLabels()
    {
        // Arrange
        using var db  = TestDbContextFactory.Create();
        var owner     = new UserBuilder().WithRole(UserRole.Owner).Build();
        var project   = new ProjectBuilder().Build();
        db.Users.Add(owner);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var service = CreateService(db, owner.Id);
        var createResult = await service.CreateAsync(
            new CreateWorkItemRequest(project.Id, "Tarefa de teste", null, WorkItemType.Task, WorkItemPriority.Medium, WorkItemDifficulty.Medium, WorkItemEstimatedTime.OneDay, null, null));
        var workItemId = createResult.Value;

        await service.UpdateLabelsAsync(workItemId, new[] { "Label1", "Label2" });
        var before = await db.WorkItemLabels.Where(wl => wl.WorkItemId == workItemId).ToListAsync();
        before.Should().HaveCount(2);

        // Act
        var updateResult = await service.UpdateLabelsAsync(workItemId, Array.Empty<string>());

        // Assert
        updateResult.Succeeded.Should().BeTrue();
        var after = await db.WorkItemLabels.Where(wl => wl.WorkItemId == workItemId).ToListAsync();
        after.Should().BeEmpty();
    }
}
