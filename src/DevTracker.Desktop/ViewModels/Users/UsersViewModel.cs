using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Users;
using DevTracker.Application.Security;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Users;

/// <summary>
/// Lista de utilizadores com opções de criar, mudar role e desactivar.
/// Permissões carregadas uma vez no OnActivatedAsync.
/// </summary>
public sealed partial class UsersViewModel(
    IUserService               userService,
    IPermissionSnapshotService permissionSnapshot,
    IDialogService             dialogService) : PageViewModelBase
{
    [ObservableProperty] private ObservableCollection<UserSummaryDto> _users = [];
    [ObservableProperty] private bool _canCreate;
    [ObservableProperty] private bool _canChangeRole;
    [ObservableProperty] private bool _canDeactivate;

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        var permissions = await permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
        CanCreate     = permissions.Contains(Permissions.UserCreate);
        CanChangeRole = permissions.Contains(Permissions.UserChangeRole);
        CanDeactivate = permissions.Contains(Permissions.UserUpdate);
        await LoadAsync(ct);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await userService.GetAllAsync(ct);
            if (!result.Succeeded) { SetError(result.Error!); return; }
            Users = new ObservableCollection<UserSummaryDto>(result.Value!);
        }
        catch (AuthorizationException) { SetError("No permission to list users."); }
        catch (Exception ex)           { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(CancellationToken.None);

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateUserAsync()
    {
        var created = await dialogService.ShowDialogAsync<CreateUserDialogViewModel>();
        if (created) await LoadAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private async Task DeactivateUserAsync(UserSummaryDto user)
    {
        var confirmed = await dialogService.ConfirmAsync(
            "Deactivate user", $"Deactivate '{user.Username}'?");
        if (!confirmed) return;

        var reAuth = await dialogService.ShowDialogAsync<Auth.ReAuthenticateDialogViewModel>();
        if (!reAuth) return;

        IsBusy = true;
        try
        {
            var result = await userService.DeactivateAsync(user.Id);
            if (!result.Succeeded) { SetError(result.Error!); return; }
            await LoadAsync(CancellationToken.None);
        }
        catch (AuthorizationException) { SetError("No permission."); }
        finally { IsBusy = false; }
    }

    partial void OnCanCreateChanged(bool value)     => CreateUserCommand.NotifyCanExecuteChanged();
    partial void OnCanDeactivateChanged(bool value)  => DeactivateUserCommand.NotifyCanExecuteChanged();
}
