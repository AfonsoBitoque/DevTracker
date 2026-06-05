using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Security;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Projects;

/// <summary>
/// Lista de projetos. Carrega permissões uma vez no OnActivatedAsync para evitar N queries.
/// Comandos desativados via CanExecute quando o utilizador não tem permissão.
/// </summary>
public sealed partial class ProjectListViewModel(
    IProjectService            projectService,
    IPermissionSnapshotService permissionSnapshot,
    INavigationService         navigation,
    IDialogService             dialogService) : PageViewModelBase
{
    [ObservableProperty] private ObservableCollection<ProjectSummaryDto> _projects = [];
    [ObservableProperty] private bool _canCreate;
    [ObservableProperty] private bool _canUpdate;
    [ObservableProperty] private bool _canDelete;

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        var permissions = await permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
        CanCreate = permissions.Contains(Permissions.ProjectCreate);
        CanUpdate = permissions.Contains(Permissions.ProjectUpdate);
        CanDelete = permissions.Contains(Permissions.ProjectDelete);

        await LoadProjectsAsync(ct);
    }

    private async Task LoadProjectsAsync(CancellationToken ct)
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await projectService.GetAllAsync(ct);
            if (!result.Succeeded) { SetError(result.Error!); return; }

            Projects = new ObservableCollection<ProjectSummaryDto>(result.Value!);
        }
        catch (AuthorizationException) { SetError("No permission to list projects."); }
        catch (Exception ex)           { SetError($"Error loading projects: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenProject(ProjectSummaryDto project)
        => navigation.NavigateTo<ProjectDetailViewModel>(vm => vm.ProjectId = project.Id);

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateProjectAsync()
    {
        var created = await dialogService.ShowDialogAsync<CreateProjectDialogViewModel>();
        if (created) await LoadProjectsAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteProjectAsync(ProjectSummaryDto project)
    {
        var confirmed = await dialogService.ConfirmAsync(
            "Delete project", $"Are you sure you want to delete '{project.Name}'?");
        if (!confirmed) return;

        var reAuth = await dialogService.ShowDialogAsync<Auth.ReAuthenticateDialogViewModel>();
        if (!reAuth) return;

        IsBusy = true;
        try
        {
            var result = await projectService.DeleteAsync(project.Id);
            if (!result.Succeeded) { SetError(result.Error!); return; }

            Projects.Remove(project);
            SetSuccess("Project deleted successfully.");
        }
        catch (AuthorizationException) { SetError("No permission to delete projects."); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task EditProjectAsync(ProjectSummaryDto project)
    {
        var saved = await dialogService.ShowDialogAsync<EditProjectDialogViewModel>(vm =>
        {
            vm.ProjectId = project.Id;
            vm.Name = project.Name;
            vm.Description = project.Description ?? string.Empty;
            vm.Color = project.Color;
            vm.GitHubRepositoryUrl = project.GitHubRepositoryUrl ?? string.Empty;
        });

        if (saved) await LoadProjectsAsync(CancellationToken.None);
    }

    partial void OnCanCreateChanged(bool value) => CreateProjectCommand.NotifyCanExecuteChanged();
    partial void OnCanUpdateChanged(bool value) => EditProjectCommand.NotifyCanExecuteChanged();
    partial void OnCanDeleteChanged(bool value)  => DeleteProjectCommand.NotifyCanExecuteChanged();
}
