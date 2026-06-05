using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Dashboard;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

public sealed class DashboardService(
    AppDbContext db,
    ICurrentUserService currentUser) : IDashboardService
{
    public async Task<Result<DashboardMetricsDto>> GetUserMetricsAsync(CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue) return Result<DashboardMetricsDto>.Fail("Not authenticated.");

        var userId = currentUser.UserId.Value;

        // Projects
        var totalProjects = await db.Projects.CountAsync(p => !p.IsDeleted, ct);
        var activeProjects = await db.Projects.CountAsync(
            p => !p.IsDeleted && (p.State == ProjectState.NotStarted || p.State == ProjectState.InProgress), ct);

        // Tarefas por status
        var completedTasks = await db.WorkItems.CountAsync(
            w => !w.IsDeleted && w.Status == WorkItemStatus.Done, ct);
        var pendingTasks = await db.WorkItems.CountAsync(
            w => !w.IsDeleted && (w.Status == WorkItemStatus.Backlog || w.Status == WorkItemStatus.Todo), ct);
        var inProgressTasks = await db.WorkItems.CountAsync(
            w => !w.IsDeleted && w.Status == WorkItemStatus.InProgress, ct);

        // Percentagem de uso de IA (tasks concluídas com prompt vs total concluídas)
        var aiUsagePercentage = completedTasks > 0
            ? await db.WorkItems.CountAsync(
                w => !w.IsDeleted && w.Status == WorkItemStatus.Done && w.AiPromptUsed != null, ct) * 100.0 / completedTasks
            : 0.0;

        // Horas estimadas (tasks por fazer)
        var pendingWorkItems = await db.WorkItems
            .Where(w => !w.IsDeleted && (w.Status == WorkItemStatus.Backlog || w.Status == WorkItemStatus.Todo))
            .ToListAsync(ct);
        var estimatedHours = pendingWorkItems.Sum(w => ConvertToHours(w.EstimatedTime));

        // Breakdown por dificuldade (tasks por fazer)
        var veryEasy = pendingWorkItems.Count(w => w.Difficulty == WorkItemDifficulty.VeryEasy);
        var easy = pendingWorkItems.Count(w => w.Difficulty == WorkItemDifficulty.Easy);
        var medium = pendingWorkItems.Count(w => w.Difficulty == WorkItemDifficulty.Medium);
        var hard = pendingWorkItems.Count(w => w.Difficulty == WorkItemDifficulty.Hard);
        var veryHard = pendingWorkItems.Count(w => w.Difficulty == WorkItemDifficulty.VeryHard);

        // Breakdown por prioridade (tasks por fazer)
        var low = pendingWorkItems.Count(w => w.Priority == WorkItemPriority.Low);
        var priorityMedium = pendingWorkItems.Count(w => w.Priority == WorkItemPriority.Medium);
        var high = pendingWorkItems.Count(w => w.Priority == WorkItemPriority.High);
        var critical = pendingWorkItems.Count(w => w.Priority == WorkItemPriority.Critical);

        // Breakdown por tipo (todas as tasks)
        var allWorkItems = await db.WorkItems.Where(w => !w.IsDeleted).ToListAsync(ct);
        var task = allWorkItems.Count(w => w.Type == WorkItemType.Task);
        var feature = allWorkItems.Count(w => w.Type == WorkItemType.Feature);
        var bug = allWorkItems.Count(w => w.Type == WorkItemType.Bug);
        var improvement = allWorkItems.Count(w => w.Type == WorkItemType.Improvement);
        var documentation = allWorkItems.Count(w => w.Type == WorkItemType.Documentation);

        var metrics = new DashboardMetricsDto(
            TotalProjects: totalProjects,
            ActiveProjects: activeProjects,
            CompletedTasks: completedTasks,
            PendingTasks: pendingTasks,
            InProgressTasks: inProgressTasks,
            EstimatedHoursRemaining: estimatedHours,
            AiUsagePercentage: aiUsagePercentage,
            TaskBreakdown: new TaskBreakdownDto(
                Difficulty: new DifficultyBreakdownDto(veryEasy, easy, medium, hard, veryHard),
                Priority: new PriorityBreakdownDto(low, priorityMedium, high, critical),
                Type: new TypeBreakdownDto(task, feature, bug, improvement, documentation)));

        return Result<DashboardMetricsDto>.Success(metrics);
    }

    private static double ConvertToHours(WorkItemEstimatedTime estimatedTime) => estimatedTime switch
    {
        WorkItemEstimatedTime.LessThan1Hour => 0.5,
        WorkItemEstimatedTime.OneToTwoHours => 1.5,
        WorkItemEstimatedTime.HalfDay => 4,
        WorkItemEstimatedTime.OneDay => 8,
        WorkItemEstimatedTime.TwoToThreeDays => 20,
        WorkItemEstimatedTime.MoreThanThreeDays => 40,
        _ => 0
    };
}
