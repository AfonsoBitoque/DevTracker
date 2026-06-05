using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Desktop.ViewModels.Base;
using DevTracker.Desktop.ViewModels.Shell;

namespace DevTracker.Desktop.ViewModels.Auth;

/// <summary>
/// Setup inicial da aplicação. Cobre dois casos:
/// 1. Workspace ainda não configurado → pedir WorkspacePath e reiniciar (IAppPaths é imutável)
/// 2. Workspace ok, sem utilizadores → criar primeiro Owner e navegar para Login sem reiniciar
/// </summary>
public sealed partial class FirstRunSetupViewModel(
    ISettingsService  settingsService,
    IWorkspaceService workspaceService,
    IAuthService      authService,
    IAppPaths         appPaths,
    ShellViewModel    shell) : PageViewModelBase
{
    [ObservableProperty] private string _workspacePath   = string.Empty;
    [ObservableProperty] private string _username        = string.Empty;
    [ObservableProperty] private string _password        = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool   _isWorkspaceStep;

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        var anyUsers = await authService.AnyUsersExistAsync(ct);
        if (anyUsers)
        {
            shell.Navigation.NavigateTo<LoginViewModel>();
            return;
        }

        IsWorkspaceStep = !appPaths.IsWorkspaceConfigured;
    }

    [RelayCommand]
    private async Task SetupAsync()
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            if (IsWorkspaceStep)
                await ConfigureWorkspaceAsync();
            else
                await CreateFirstOwnerAsync();
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task ConfigureWorkspaceAsync()
    {
        var wsResult = await workspaceService.EnsureWorkspaceRootAsync(WorkspacePath.Trim());
        if (!wsResult.Succeeded) { SetError(wsResult.Error!); return; }

        var settings = await settingsService.LoadAsync();
        settings.WorkspaceRoot = wsResult.Value!;
        var saveResult = await settingsService.SaveAsync(settings);
        if (!saveResult.Succeeded) { SetError(saveResult.Error!); return; }

        // Reinício obrigatório — IAppPaths é Singleton imutável, precisa de ser reconstruído
        RestartApp();
    }

    private async Task CreateFirstOwnerAsync()
    {
        if (string.IsNullOrWhiteSpace(Username)) { SetError("Username is required."); return; }
        if (Password != ConfirmPassword)         { SetError("Passwords do not match."); return; }

        var result = await authService.SetupFirstOwnerAsync(Username.Trim(), Password);
        if (!result.Succeeded) { SetError(result.Error!); return; }

        // Owner criado com sucesso — navegar para Login sem reiniciar
        shell.Navigation.NavigateTo<LoginViewModel>();
    }

    /// <summary>Reinicia o processo da app (necessário só após mudança de WorkspaceRoot).</summary>
    private static void RestartApp()
    {
        var exe = Environment.ProcessPath;
        if (exe is not null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        Environment.Exit(0);
    }
}
