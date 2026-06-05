using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Application.Security;
using DevTracker.Core.Enums;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.WorkItems;

public sealed partial class WorkItemDetailViewModel(
    IWorkItemService           workItemService,
    IProjectService            projectService,
    IGitService                gitService,
    IGitHubService             gitHubService,
    ISecretService             secretService,
    IPermissionSnapshotService permissionSnapshot,
    INavigationService         navigation,
    IDialogService             dialogService,
    IAppNotificationService    notificationService,
    IAiContextService          aiContextService) : PageViewModelBase
{
    public Guid WorkItemId { get; set; }

    [ObservableProperty] private WorkItemDetailDto? _workItem;
    [ObservableProperty] private string             _newComment   = string.Empty;
    [ObservableProperty] private bool               _canUpdate;
    [ObservableProperty] private bool               _canDelete;
    [ObservableProperty] private bool               _canComment;
    [ObservableProperty] private bool               _canChangeStatus;
    [ObservableProperty] private bool               _canStartTask;
    [ObservableProperty] private bool               _canFinishTask;
    [ObservableProperty] private bool               _canCreatePullRequest;
    [ObservableProperty] private bool               _canPauseTask;

    /// <summary>Disparado quando o texto gerado deve ser copiado para o Clipboard.</summary>
    public event Action<string>? CopyToClipboardRequested;

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        var permissions  = await permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
        CanUpdate        = permissions.Contains(Permissions.WorkItemUpdate);
        CanDelete        = permissions.Contains(Permissions.WorkItemDelete);
        CanComment       = permissions.Contains(Permissions.WorkItemComment);
        CanChangeStatus  = permissions.Contains(Permissions.WorkItemChangeStatus);
        CanStartTask     = permissions.Contains(Permissions.WorkItemChangeStatus);
        CanFinishTask    = permissions.Contains(Permissions.WorkItemChangeStatus);
        CanPauseTask     = permissions.Contains(Permissions.WorkItemChangeStatus) && WorkItem?.Status == WorkItemStatus.InProgress;

        await LoadAsync(ct);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await workItemService.GetByIdAsync(WorkItemId, ct);
            if (!result.Succeeded) { SetError(result.Error!); return; }
            WorkItem = result.Value;
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateAsync()
    {
        var updated = await dialogService.ShowDialogAsync<UpdateWorkItemDialogViewModel>(
            async vm => 
            {
                vm.WorkItemId = WorkItemId;
                await vm.LoadAsync(CancellationToken.None);
            });
        if (updated) await LoadAsync(CancellationToken.None);
    }

    partial void OnCanUpdateChanged(bool value) => UpdateCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var confirmed = await dialogService.ConfirmAsync(
            "Delete Work Item", "Are you sure? This action cannot be undone.");
        if (!confirmed) return;

        var reAuth = await dialogService.ShowDialogAsync<Auth.ReAuthenticateDialogViewModel>();
        if (!reAuth) return;

        IsBusy = true;
        try
        {
            var result = await workItemService.DeleteAsync(WorkItemId);
            if (!result.Succeeded) { SetError(result.Error!); return; }
            navigation.NavigateBack();
        }
        catch (AuthorizationException) { SetError("No permission to delete work items."); }
        finally { IsBusy = false; }
    }

    partial void OnCanDeleteChanged(bool value) => DeleteCommand.NotifyCanExecuteChanged();
    partial void OnCanChangeStatusChanged(bool value) => ChangeStatusCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task ChangeStatusAsync(string statusStr)
    {
        if (!Enum.TryParse<WorkItemStatus>(statusStr, out var newStatus))
        {
            SetError("Invalid state.");
            return;
        }
        var result = await workItemService.ChangeStatusAsync(new ChangeWorkItemStatusRequest(WorkItemId, newStatus));
        if (!result.Succeeded) { SetError(result.Error!); return; }
        await LoadAsync(CancellationToken.None);
    }

    private bool CanAddComment => CanComment && !string.IsNullOrWhiteSpace(NewComment);

    partial void OnNewCommentChanged(string value) => AddCommentCommand.NotifyCanExecuteChanged();
    partial void OnCanCommentChanged(bool value) => AddCommentCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanAddComment))]
    private async Task AddCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewComment)) return;

        IsBusy = true;
        try
        {
            var result = await workItemService.AddCommentAsync(new AddCommentRequest(WorkItemId, NewComment.Trim()));
            if (!result.Succeeded) { SetError(result.Error!); return; }

            NewComment = string.Empty;
            await LoadAsync(CancellationToken.None);
        }
        catch (AuthorizationException) { SetError("No permission to comment."); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanStartTask))]
    private async Task StartTaskAsync()
    {
        if (WorkItem is null) return;

        SetBusy(true, "Initializing local repository and creating branch...");
        try
        {
            var projectResult = await projectService.GetByIdAsync(WorkItem.ProjectId);
            if (!projectResult.Succeeded) { notificationService.ShowError("Error starting task", projectResult.Error!); return; }

            var repoPath = projectResult.Value!.WorkspacePath;
            if (!string.IsNullOrWhiteSpace(repoPath) && Directory.Exists(repoPath))
            {
                var initResult = await gitService.InitAsync(repoPath);
                if (!initResult.Succeeded) { notificationService.ShowError("Error starting task", $"Failed to initialize git: {initResult.Error}"); return; }

                if (!string.IsNullOrWhiteSpace(projectResult.Value.GitHubRepositoryUrl))
                {
                    var remoteResult = await gitService.AddRemoteAsync(repoPath, "origin", projectResult.Value.GitHubRepositoryUrl);
                    if (!remoteResult.Succeeded) { notificationService.ShowError("Error starting task", $"Failed to add remote: {remoteResult.Error}"); return; }
                }

                var branchName = $"feature/task-{WorkItem.Number}-{WorkItem.Title}";
                var branchResult = await gitService.CreateBranchAsync(repoPath, branchName);
                if (!branchResult.Succeeded) { notificationService.ShowError("Error starting task", $"Failed to create branch: {branchResult.Error}"); return; }

                await gitService.AddAllAsync(repoPath);
                var commitResult = await gitService.CommitAsync(repoPath, $"Snapshot before starting task: {WorkItem.Title}");
                if (!commitResult.Succeeded) { notificationService.ShowError("Error starting task", $"Failed to commit: {commitResult.Error}"); return; }
            }

            var result = await workItemService.ChangeStatusAsync(
                new ChangeWorkItemStatusRequest(WorkItemId, WorkItemStatus.InProgress));
            if (!result.Succeeded) { notificationService.ShowError("Error starting task", result.Error!); return; }

            notificationService.ShowSuccess("Task started", $"Work item #{WorkItem.Number} started successfully.");
            await LoadAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error starting task", ex.Message);
        }
        finally { SetBusy(false); }
    }

    partial void OnCanStartTaskChanged(bool value) => Avalonia.Threading.Dispatcher.UIThread.Post(() => StartTaskCommand.NotifyCanExecuteChanged());

    [RelayCommand(CanExecute = nameof(CanCreatePullRequest))]
    private async Task CreatePullRequestAsync()
    {
        if (WorkItem is null) return;

        var projectResult = await projectService.GetByIdAsync(WorkItem.ProjectId);
        if (!projectResult.Succeeded) { notificationService.ShowError("Error creating PR", projectResult.Error!); return; }

        var project = projectResult.Value!;
        if (string.IsNullOrWhiteSpace(project.GitHubRepositoryUrl))
        {
            notificationService.ShowWarning("No GitHub URL", "Project has no GitHub URL configured.");
            return;
        }

        var repoPath = project.WorkspacePath;
        if (string.IsNullOrWhiteSpace(repoPath) || !gitService.IsGitRepository(repoPath))
        {
            notificationService.ShowWarning("Repository not initialized", "Click 'Start Task' first.");
            return;
        }

        SetBusy(true, "Contacting GitHub API to create Pull Request...");
        try
        {
            var tokenResult = await secretService.GetSecretAsync("GitHubToken");
            if (!tokenResult.Succeeded || string.IsNullOrWhiteSpace(tokenResult.Value))
            {
                notificationService.ShowWarning("Missing token", "GitHub Token not configured. Go to Settings to add.");
                return;
            }

            // Obter o branch atual (pode ser o da Start ou da Finish)
            var branchResult = await gitService.GetCurrentBranchAsync(repoPath);
            if (!branchResult.Succeeded) { notificationService.ShowError("Error creating PR", $"Failed to get branch: {branchResult.Error}"); return; }
            var headBranch = branchResult.Value?.Trim() ?? $"feature/task-{WorkItem.Number}-{WorkItem.Title}";

            // Empurrar para o remoto antes de criar o PR (GitHub 422 se não existir)
            var pushResult = await gitService.PushAsync(repoPath);
            if (!pushResult.Succeeded)
            {
                notificationService.ShowError("Push failed", $"Could not send branch to GitHub: {pushResult.Error}");
                return;
            }

            var title = $"#{WorkItem.Number} {WorkItem.Title}";
            var body = WorkItem.Description ?? $"Pull request for task #{WorkItem.Number}.";

            var defaultBranchResult = await gitHubService.GetDefaultBranchAsync(project.GitHubRepositoryUrl, tokenResult.Value!);
            if (!defaultBranchResult.Succeeded) { notificationService.ShowError("Error creating PR", $"Failed to get default branch: {defaultBranchResult.Error}"); return; }
            var baseBranch = defaultBranchResult.Value!.Trim();

            var result = await gitHubService.CreatePullRequestAsync(
                project.GitHubRepositoryUrl,
                title,
                body,
                headBranch,
                baseBranch,
                tokenResult.Value!);

            if (!result.Succeeded)
            {
                notificationService.ShowError("Failed to create PR", result.Error!);
                return;
            }

            notificationService.ShowSuccess("Pull Request created", result.Value!);
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error creating PR", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    partial void OnCanCreatePullRequestChanged(bool value) => CreatePullRequestCommand.NotifyCanExecuteChanged();

    private void UpdateCanCreatePullRequest()
    {
        CanCreatePullRequest = WorkItem is not null
            && !string.IsNullOrWhiteSpace(WorkItem.ProjectName)
            && WorkItem.Status is WorkItemStatus.InProgress or WorkItemStatus.InReview;
    }

    [RelayCommand(CanExecute = nameof(CanFinishTask))]
    private async Task FinishTaskAsync()
    {
        SetBusy(true, "Extracting diffs and sending code to GitHub...");
        try
        {
            if (WorkItem is null) return;

            var projectResult = await projectService.GetByIdAsync(WorkItem.ProjectId);
            if (!projectResult.Succeeded) { notificationService.ShowError("Error finishing task", projectResult.Error!); return; }

            var repoPath = projectResult.Value!.WorkspacePath;
            if (string.IsNullOrWhiteSpace(repoPath) || !gitService.IsGitRepository(repoPath))
            {
                notificationService.ShowWarning("Repository not initialized", "Click 'Start Task' first.");
                return;
            }

            var diffResult = await gitService.GetDiffAsync(repoPath);
            if (!diffResult.Succeeded) { notificationService.ShowError("Error finishing task", $"Failed to get diff: {diffResult.Error}"); return; }

            var diffContent = diffResult.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(diffContent))
            {
                notificationService.ShowInfo("No changes", "There are no changes to show or commit.");
                return;
            }

            var dialogResult = await dialogService.ShowDialogAsync<DiffViewModel, DiffResult?>(
                vm => vm.SetDiff(diffContent, WorkItem.Title, WorkItem.Description ?? string.Empty));
            if (dialogResult is null) return;

            await gitService.AddAllAsync(repoPath);
            var commitResult = await gitService.CommitAsync(repoPath, $"Complete task: {WorkItem.Title}");
            if (!commitResult.Succeeded) { notificationService.ShowError("Error finishing task", $"Failed to commit: {commitResult.Error}"); return; }

            if (!string.IsNullOrWhiteSpace(projectResult.Value.GitHubRepositoryUrl))
            {
                var pushResult = await gitService.PushAsync(repoPath);
                if (!pushResult.Succeeded)
                {
                    notificationService.ShowError("Push failed", pushResult.Error!);
                    return;
                }
            }

            var statusResult = await workItemService.ChangeStatusAsync(
                new ChangeWorkItemStatusRequest(WorkItemId, WorkItemStatus.Done,
                    dialogResult.AiPrompt, ActualPrompts: dialogResult.ActualPrompts));
            if (!statusResult.Succeeded) { notificationService.ShowError("Error finishing task", statusResult.Error!); return; }

            notificationService.ShowSuccess("Task completed", $"Work item #{WorkItem.Number} finished successfully.");
            await LoadAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error finishing task", ex.Message);
        }
        finally { SetBusy(false); }
    }

    partial void OnCanFinishTaskChanged(bool value) => Avalonia.Threading.Dispatcher.UIThread.Post(() => FinishTaskCommand.NotifyCanExecuteChanged());
    partial void OnCanPauseTaskChanged(bool value) => Avalonia.Threading.Dispatcher.UIThread.Post(() => PauseTaskCommand.NotifyCanExecuteChanged());

    partial void OnWorkItemChanged(WorkItemDetailDto? value)
    {
        CanPauseTask = value?.Status == WorkItemStatus.InProgress;
        CanStartTask = value?.Status is WorkItemStatus.Backlog or WorkItemStatus.Todo;
        UpdateCanCreatePullRequest();
    }

    [RelayCommand(CanExecute = nameof(CanPauseTask))]
    private async Task PauseTaskAsync()
    {
        if (WorkItem is null) return;

        var note = await dialogService.ShowDialogAsync<PauseTaskDialogViewModel, string?>();
        if (note is null) return;

        SetBusy(true, "Pausing task...");
        try
        {
            var result = await workItemService.ChangeStatusAsync(
                new ChangeWorkItemStatusRequest(WorkItemId, WorkItemStatus.Todo, MomentumNote: note));
            if (!result.Succeeded) { SetError(result.Error!); return; }

            notificationService.ShowSuccess("Task paused", "Task moved to Todo with a momentum note.");
            await LoadAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error pausing task", ex.Message);
        }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    private async Task CopyAiContextAsync()
    {
        SetBusy(true, "Packaging context for AI...");
        try
        {
            var result = await aiContextService.PackTaskContextAsync(WorkItemId);
            if (!result.Succeeded) { SetError(result.Error!); return; }

            CopyToClipboardRequested?.Invoke(result.Value!);
            notificationService.ShowSuccess("Context Copied", "The Markdown block was saved to your Clipboard. You can paste it directly into Windsurf/Kimi!");
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error copying context", ex.Message);
        }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    private async Task ManageLabelsAsync()
    {
        if (WorkItem is null) return;

        var selectedLabels = await dialogService.ShowDialogAsync<ManageLabelsDialogViewModel, IReadOnlyList<string>>(
            vm =>
            {
                vm.WorkItemId = WorkItemId;
                vm.Initialize(WorkItem.Labels, WorkItem.Labels);
            });

        if (selectedLabels is null) return;

        SetBusy(true, "Saving labels...");
        try
        {
            var result = await workItemService.UpdateLabelsAsync(WorkItemId, selectedLabels);
            if (!result.Succeeded) { SetError(result.Error!); return; }

            notificationService.ShowSuccess("Labels updated", "Task labels were saved successfully.");
            await LoadAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error saving labels", ex.Message);
        }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    private void GoBack() => navigation.NavigateBack();
}
