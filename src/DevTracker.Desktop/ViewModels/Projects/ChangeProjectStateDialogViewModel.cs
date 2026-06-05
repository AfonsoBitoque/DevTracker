using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Core.Enums;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Projects;

public sealed partial class ChangeProjectStateDialogViewModel(
    IProjectService projectService) : DialogViewModelBase
{
    public Guid ProjectId { get; set; }

    [ObservableProperty] private ProjectState _newState = ProjectState.InProgress;
    [ObservableProperty] private string       _reason   = string.Empty;

    /// <summary>Reason é obrigatório para Paused e Cancelled — validado no serviço via FluentValidation.</summary>
    public bool RequiresReason => NewState is ProjectState.Paused or ProjectState.Cancelled;

    partial void OnNewStateChanged(ProjectState value) => OnPropertyChanged(nameof(RequiresReason));

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await projectService.ChangeStateAsync(
                new ChangeProjectStateRequest(ProjectId, NewState, Reason.Trim() is "" ? null : Reason.Trim()));

            if (!result.Succeeded) { SetError(result.Error!); return; }
            Confirm();
        }
        catch (AuthorizationException) { SetError("No permission to change state."); }
        catch (Exception ex)           { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CancelDialog() => Cancel();
}
