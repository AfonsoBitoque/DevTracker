using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Security;
using DevTracker.Core.Enums;
using DevTracker.Desktop.ViewModels.Auth;
using DevTracker.Desktop.ViewModels.Base;
using DevTracker.Desktop.ViewModels.WorkItems;
using WorkItemDtos = DevTracker.Application.DTOs.WorkItems;

namespace DevTracker.Desktop.ViewModels.Projects;

public sealed partial class ProjectDetailViewModel(
    IProjectService            projectService,
    IWorkItemService           workItemService,
    IPermissionSnapshotService permissionSnapshot,
    INavigationService         navigation,
    IDialogService             dialogService,
    IWorkspaceService          workspaceService,
    IAppNotificationService    notificationService) : PageViewModelBase
{
    public Guid ProjectId { get; set; }

    [ObservableProperty] private ProjectDetailDto? _project;
    [ObservableProperty] private bool _canUpdate;
    [ObservableProperty] private bool _canChangeState;
    [ObservableProperty] private bool _canDelete;
    [ObservableProperty] private bool _canCreateWorkItem;
    [ObservableProperty] private ObservableCollection<FileEntryViewModel> _files = [];
    [ObservableProperty] private ObservableCollection<WorkItemDtos.WorkItemSummaryDto> _activeItems = [];
    [ObservableProperty] private ObservableCollection<WorkItemDtos.WorkItemSummaryDto> _todoItems = [];
    [ObservableProperty] private ObservableCollection<WorkItemDtos.WorkItemSummaryDto> _doneItems = [];
    [ObservableProperty] private int _totalEstimatedPrompts;
    [ObservableProperty] private int _totalActualPrompts;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private WorkItemType? _selectedTypeFilter;

    private IReadOnlyList<WorkItemDtos.WorkItemSummaryDto> _allItems = [];

    public static IReadOnlyList<WorkItemType?> AvailableTypes { get; } =
        new List<WorkItemType?> { null }
            .Concat(Enum.GetValues<WorkItemType>().Cast<WorkItemType?>())
            .ToList();

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        permissionSnapshot.InvalidateCache();
        var permissions = await permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
        CanUpdate          = permissions.Contains(Permissions.ProjectUpdate);
        CanChangeState     = permissions.Contains(Permissions.ProjectChangeState);
        CanDelete          = permissions.Contains(Permissions.ProjectDelete);
        CanCreateWorkItem  = permissions.Contains(Permissions.WorkItemCreate);

        await LoadAsync(ct);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await projectService.GetByIdAsync(ProjectId, ct);
            if (!result.Succeeded) { SetError(result.Error!); return; }
            Project = result.Value;
            await LoadFilesAsync(result.Value!.WorkspacePath);
            FilterWorkItems(result.Value!.WorkItems);
        }
        catch (AuthorizationException) { SetError("No permission."); }
        catch (Exception ex)           { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task SaveAiNameAsync()
    {
        if (Project is null) return;

        IsBusy = true;
        try
        {
            var result = await projectService.UpdateAsync(new UpdateProjectRequest(
                Project.Id,
                Project.Name,
                Project.Description,
                Project.Color,
                Project.GitHubRepositoryUrl,
                Project.UsedAiName));
            if (!result.Succeeded) { SetError(result.Error!); return; }
            await LoadAsync(CancellationToken.None);
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    partial void OnCanUpdateChanged(bool value)
    {
        SaveAiNameCommand.NotifyCanExecuteChanged();
        EditProjectCommand.NotifyCanExecuteChanged();
    }
    partial void OnCanDeleteChanged(bool value) => DeleteProjectCommand.NotifyCanExecuteChanged();
    partial void OnCanChangeStateChanged(bool value) => ChangeStateCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task EditProjectAsync()
    {
        if (Project is null) return;

        var saved = await dialogService.ShowDialogAsync<EditProjectDialogViewModel>(vm =>
        {
            vm.ProjectId = Project.Id;
            vm.Name = Project.Name;
            vm.Description = Project.Description ?? string.Empty;
            vm.Color = Project.Color;
            vm.GitHubRepositoryUrl = Project.GitHubRepositoryUrl ?? string.Empty;
        });

        if (saved) await LoadAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteProjectAsync()
    {
        if (Project is null) return;

        if (!await dialogService.ConfirmAsync("Delete Project", $"Are you sure you want to delete '{Project.Name}'? This action cannot be undone."))
            return;

        var result = await dialogService.ShowDialogAsync<ReAuthenticateDialogViewModel>();
        if (!result) return;

        var deleteResult = await projectService.DeleteAsync(Project.Id);
        if (!deleteResult.Succeeded) { SetError(deleteResult.Error!); return; }

        notificationService.ShowSuccess("Project deleted", $"'{Project.Name}' was deleted successfully.");
        navigation.NavigateBack();
    }

    [RelayCommand(CanExecute = nameof(CanCreateWorkItem))]
    private async Task CreateWorkItemAsync()
    {
        var created = await dialogService.ShowDialogAsync<CreateWorkItemDialogViewModel>(
            vm => vm.ProjectId = ProjectId);
        if (created) await LoadAsync(CancellationToken.None);
    }

    partial void OnCanCreateWorkItemChanged(bool value) => CreateWorkItemCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void OpenWorkItem(WorkItemDtos.WorkItemSummaryDto item)
        => navigation.NavigateTo<WorkItemDetailViewModel>(vm => vm.WorkItemId = item.Id);

    [RelayCommand]
    private async Task MoveToActiveAsync(WorkItemDtos.WorkItemSummaryDto item)
    {
        var result = await workItemService.ChangeStatusAsync(
            new WorkItemDtos.ChangeWorkItemStatusRequest(item.Id, WorkItemStatus.InProgress));
        if (!result.Succeeded) { SetError(result.Error!); return; }
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task MoveToTodoAsync(WorkItemDtos.WorkItemSummaryDto item)
    {
        var result = await workItemService.ChangeStatusAsync(
            new WorkItemDtos.ChangeWorkItemStatusRequest(item.Id, WorkItemStatus.Todo));
        if (!result.Succeeded) { SetError(result.Error!); return; }
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task MoveToDoneAsync(WorkItemDtos.WorkItemSummaryDto item)
    {
        var result = await workItemService.ChangeStatusAsync(
            new WorkItemDtos.ChangeWorkItemStatusRequest(item.Id, WorkItemStatus.Done));
        if (!result.Succeeded) { SetError(result.Error!); return; }
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanChangeState))]
    private async Task ChangeStateAsync()
    {
        await dialogService.ShowDialogAsync<ChangeProjectStateDialogViewModel>(
            vm => vm.ProjectId = ProjectId);
        await LoadAsync(CancellationToken.None);
    }

    private void FilterWorkItems(IReadOnlyList<WorkItemDtos.WorkItemSummaryDto> allItems)
    {
        _allItems = allItems;
        var filtered = allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim();
            filtered = filtered.Where(i =>
                i.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Number.ToString().Contains(query));
        }

        if (SelectedTypeFilter.HasValue)
        {
            filtered = filtered.Where(i => i.Type == SelectedTypeFilter.Value);
        }

        var items = filtered.ToList();
        ActiveItems = new ObservableCollection<WorkItemDtos.WorkItemSummaryDto>(
            items.Where(i => i.Status == WorkItemStatus.InProgress));
        TodoItems = new ObservableCollection<WorkItemDtos.WorkItemSummaryDto>(
            items.Where(i => i.Status == WorkItemStatus.Todo || i.Status == WorkItemStatus.Backlog));
        DoneItems = new ObservableCollection<WorkItemDtos.WorkItemSummaryDto>(
            items.Where(i => i.Status == WorkItemStatus.Done));

        TotalEstimatedPrompts = DoneItems.Sum(i => i.EstimatedPrompts);
        TotalActualPrompts = DoneItems.Sum(i => i.ActualPrompts);
    }

    partial void OnSearchQueryChanged(string value) => FilterWorkItems(_allItems);
    partial void OnSelectedTypeFilterChanged(WorkItemType? value) => FilterWorkItems(_allItems);

    private async Task LoadFilesAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Files = [];
            return;
        }

        var result = await workspaceService.ListEntriesAsync(path);
        if (!result.Succeeded)
        {
            Files = [];
            return;
        }

        Files = new ObservableCollection<FileEntryViewModel>(
            result.Value!.Select(e => new FileEntryViewModel(e.Name, e.FullPath, e.IsDirectory)));
    }

    [RelayCommand]
    private async Task ReloadAsync() => await LoadAsync(CancellationToken.None);

    [RelayCommand]
    private void GoBack() => navigation.NavigateBack();
}
