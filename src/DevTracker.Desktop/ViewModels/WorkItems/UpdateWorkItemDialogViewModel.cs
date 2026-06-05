using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Core.Enums;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.WorkItems;

public sealed partial class UpdateWorkItemDialogViewModel(
    IWorkItemService workItemService) : DialogViewModelBase
{
    public Guid WorkItemId { get; set; }

    [ObservableProperty] private string                _title       = string.Empty;
    [ObservableProperty] private string                _description = string.Empty;
    [ObservableProperty] private WorkItemType          _type        = WorkItemType.Task;
    [ObservableProperty] private WorkItemPriority      _priority    = WorkItemPriority.Medium;
    [ObservableProperty] private WorkItemDifficulty    _difficulty  = WorkItemDifficulty.Medium;
    [ObservableProperty] private WorkItemEstimatedTime _estimatedTime = WorkItemEstimatedTime.OneDay;
    [ObservableProperty] private int                   _estimatedPrompts = 0;

    public IReadOnlyList<WorkItemType>          AvailableTypes      { get; } =
        [WorkItemType.Task, WorkItemType.Feature, WorkItemType.Bug,
         WorkItemType.Improvement, WorkItemType.Documentation];

    public IReadOnlyList<WorkItemPriority>      AvailablePriorities { get; } =
        [WorkItemPriority.Low, WorkItemPriority.Medium,
         WorkItemPriority.High, WorkItemPriority.Critical];

    public IReadOnlyList<WorkItemDifficulty>    AvailableDifficulties { get; } =
        [WorkItemDifficulty.VeryEasy, WorkItemDifficulty.Easy, WorkItemDifficulty.Medium,
         WorkItemDifficulty.Hard, WorkItemDifficulty.VeryHard];

    public IReadOnlyList<WorkItemEstimatedTime> AvailableEstimatedTimes { get; } =
        [WorkItemEstimatedTime.LessThan1Hour, WorkItemEstimatedTime.OneToTwoHours,
         WorkItemEstimatedTime.HalfDay, WorkItemEstimatedTime.OneDay,
         WorkItemEstimatedTime.TwoToThreeDays, WorkItemEstimatedTime.MoreThanThreeDays];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (WorkItemId == Guid.Empty) return;

        IsBusy = true;
        try
        {
            var result = await workItemService.GetByIdAsync(WorkItemId, ct);
            if (!result.Succeeded) return;

            var item = result.Value;
            Title         = item.Title;
            Description   = item.Description ?? string.Empty;
            Type          = item.Type;
            Priority      = item.Priority;
            Difficulty    = item.Difficulty;
            EstimatedTime = item.EstimatedTime;
            EstimatedPrompts = item.EstimatedPrompts;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) { SetError("O titulo e obrigatorio."); return; }

        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await workItemService.UpdateAsync(new UpdateWorkItemRequest(
                WorkItemId,
                Title.Trim(),
                Description.Trim() is "" ? null : Description.Trim(),
                Type,
                Priority,
                Difficulty,
                EstimatedTime,
                AssignedToUserId: null,
                DueDate: null,
                EstimatedPrompts));

            if (!result.Succeeded) { SetError(result.Error!); return; }
            Confirm();
        }
        catch (AuthorizationException) { SetError("No permission to update work items."); }
        catch (Exception ex)           { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CancelDialog() => Cancel();
}
