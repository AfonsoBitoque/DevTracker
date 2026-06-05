using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Users;
using DevTracker.Core.Enums;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Users;

/// <summary>Dialog para criar um novo utilizador (role != Owner — só via bootstrap).</summary>
public sealed partial class CreateUserDialogViewModel(
    IUserService userService) : DialogViewModelBase
{
    [ObservableProperty] private string   _username        = string.Empty;
    [ObservableProperty] private string   _password        = string.Empty;
    [ObservableProperty] private string   _confirmPassword = string.Empty;
    [ObservableProperty] private UserRole _role            = UserRole.Reader;

    public IReadOnlyList<UserRole> AvailableRoles { get; } =
        [UserRole.Admin, UserRole.Maintainer, UserRole.Reader];

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await userService.CreateAsync(
                new CreateUserRequest(Username.Trim(), Password, ConfirmPassword, Role));
            if (!result.Succeeded) { SetError(result.Error!); return; }
            Confirm();
        }
        catch (AuthorizationException) { SetError("No permission to create users."); }
        catch (Exception ex)           { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CancelDialog() => Cancel();
}
