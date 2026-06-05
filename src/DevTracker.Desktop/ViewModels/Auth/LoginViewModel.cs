using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Common;
using DevTracker.Desktop.ViewModels.Base;
using DevTracker.Desktop.ViewModels.Shell;

namespace DevTracker.Desktop.ViewModels.Auth;

/// <summary>
/// VM de login. Após autenticação bem-sucedida delega para ShellViewModel.OnAuthenticatedAsync().
/// IsReAuthMode=true é usado quando a sessão expira mas queremos manter contexto.
/// </summary>
public sealed partial class LoginViewModel(
    IAuthService  authService,
    ShellViewModel shell) : PageViewModelBase
{
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool   _isReAuthMode;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await authService.LoginAsync(Username.Trim(), Password);
            if (!result.Succeeded)
            {
                SetError(result.Error!);
                return;
            }
            await shell.OnAuthenticatedAsync();
        }
        catch (Exception ex)
        {
            SetError($"Unexpected error: {ex.Message}");
        }
        finally
        {
            Password = string.Empty;
            IsBusy = false;
        }
    }

    private bool CanLogin()
        => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsBusy;

    partial void OnUsernameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
}
