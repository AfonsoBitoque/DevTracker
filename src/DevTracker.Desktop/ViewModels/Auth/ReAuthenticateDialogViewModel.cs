using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Auth;

/// <summary>
/// Dialog de re-autenticação para operações destrutivas.
/// Fluxo: UI abre este dialog → utilizador insere password → ConfirmPasswordAsync → fecha.
/// Resultado bool indica se a re-autenticação foi bem-sucedida.
/// </summary>
public sealed partial class ReAuthenticateDialogViewModel(
    IReAuthenticationService reAuthService,
    ICurrentUserService      currentUser) : DialogViewModelBase
{
    [ObservableProperty] private string _password = string.Empty;

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (!currentUser.UserId.HasValue) { Cancel(); return; }

        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await reAuthService.ConfirmPasswordAsync(currentUser.UserId.Value, Password);
            if (!result.Succeeded) { SetError("Incorrect password."); return; }

            Password = string.Empty;
            Confirm();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CancelDialog() => Cancel();
}
