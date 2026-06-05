using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Projects;

public sealed partial class CreateProjectDialogViewModel(
    IProjectService           projectService,
    IAppNotificationService   notificationService) : DialogViewModelBase
{
    [ObservableProperty] private string _name                 = string.Empty;
    [ObservableProperty] private string _description          = string.Empty;
    [ObservableProperty] private string _color                = "#2563EB";
    [ObservableProperty] private string _gitHubRepositoryUrl = string.Empty;

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        SetBusy(true, "Creating project structure and performing git clone...");
        try
        {
            // WorkspacePath vazio — o ProjectService cria a pasta automaticamente via WorkspaceService
            var result = await projectService.CreateAsync(
                new CreateProjectRequest(
                    Name.Trim(),
                    Description.Trim() is "" ? null : Description.Trim(),
                    Color,
                    string.Empty,
                    GitHubRepositoryUrl.Trim() is "" ? null : GitHubRepositoryUrl.Trim()));

            if (!result.Succeeded) { SetError(result.Error!); return; }

            notificationService.ShowSuccess("Project created", $"'{Name.Trim()}' was created successfully.");
            Confirm();
        }
        catch (AuthorizationException)
        {
            notificationService.ShowError("Permission denied", "No permission to create projects.");
            SetError("No permission to create projects.");
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error creating project", ex.Message);
            SetError(ex.Message);
        }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    private void CancelDialog() => Cancel();
}
