using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Projects;

public sealed partial class EditProjectDialogViewModel(
    IProjectService           projectService,
    IAppNotificationService   notificationService) : DialogViewModelBase
{
    public Guid ProjectId { get; set; }

    [ObservableProperty] private string _name                 = string.Empty;
    [ObservableProperty] private string _description          = string.Empty;
    [ObservableProperty] private string _color                = "#2563EB";
    [ObservableProperty] private string _gitHubRepositoryUrl = string.Empty;

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        SetBusy(true, "Saving changes...");
        try
        {
            var result = await projectService.UpdateAsync(
                new UpdateProjectRequest(
                    ProjectId,
                    Name.Trim(),
                    Description.Trim() is "" ? null : Description.Trim(),
                    Color,
                    GitHubRepositoryUrl.Trim() is "" ? null : GitHubRepositoryUrl.Trim()));

            if (!result.Succeeded) { SetError(result.Error!); return; }

            notificationService.ShowSuccess("Project updated", $"'{Name.Trim()}' was updated successfully.");
            Confirm();
        }
        catch (AuthorizationException)
        {
            notificationService.ShowError("Permission denied", "No permission to edit projects.");
            SetError("No permission to edit projects.");
        }
        catch (Exception ex)
        {
            notificationService.ShowError("Error editing project", ex.Message);
            SetError(ex.Message);
        }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    private void CancelDialog() => Cancel();
}
