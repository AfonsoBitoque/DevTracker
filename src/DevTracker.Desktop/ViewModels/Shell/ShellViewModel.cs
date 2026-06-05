using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Application.Security;
using DevTracker.Desktop.ViewModels.Audit;
using DevTracker.Desktop.ViewModels.Auth;
using DevTracker.Desktop.ViewModels.Base;
using DevTracker.Desktop.ViewModels.Projects;
using DevTracker.Desktop.ViewModels.Users;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop.ViewModels.Shell;

public sealed partial class ShellViewModel(
    IServiceProvider serviceProvider) : ViewModelBase
{
    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private bool           _isAuthenticated;
    [ObservableProperty] private bool           _canManageUsers;
    [ObservableProperty] private bool           _canViewAudit;
    [ObservableProperty] private bool           _canGoBack;
    [ObservableProperty] private bool           _canGoForward;

    private INavigationService _navigation = null!;
    public INavigationService Navigation
    {
        get => _navigation;
        set
        {
            if (_navigation is not null) _navigation.StateChanged -= OnNavigationStateChanged;
            _navigation = value;
            if (_navigation is not null) _navigation.StateChanged += OnNavigationStateChanged;
        }
    }

    private void OnNavigationStateChanged()
    {
        CanGoBack  = Navigation.CanNavigateBack;
        CanGoForward = Navigation.CanNavigateForward;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => Navigation.NavigateBack();

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward() => Navigation.NavigateForward();

    public async Task InitializeAsync()
    {
        try
        {
            using var scope      = serviceProvider.CreateScope();
            var settingsService  = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var authService      = scope.ServiceProvider.GetRequiredService<IAuthService>();

            var isInitialized = await settingsService.IsInitializedAsync();
            if (!isInitialized)
            {
                Navigation.NavigateTo<FirstRunSetupViewModel>();
                return;
            }

            var anyUsers = await authService.AnyUsersExistAsync();
            if (!anyUsers)
            {
                Navigation.NavigateTo<FirstRunSetupViewModel>();
                return;
            }

            Navigation.NavigateTo<LoginViewModel>();
        }
        catch (Exception ex)
        {
            SetError($"Error initializing: {ex.Message}");
            Navigation.NavigateTo<LoginViewModel>();
        }
    }

    public async Task OnAuthenticatedAsync()
    {
        IsAuthenticated = true;

        using var scope       = serviceProvider.CreateScope();
        var permissionSnapshot = scope.ServiceProvider.GetRequiredService<IPermissionSnapshotService>();
        var permissions        = await permissionSnapshot.GetCurrentUserPermissionsAsync();
        CanManageUsers = permissions.Contains(Permissions.UserCreate);
        CanViewAudit   = permissions.Contains(Permissions.AuditRead);

        Navigation.NavigateTo<DashboardViewModel>();
    }

    public void OnLoggedOut()
    {
        IsAuthenticated = false;
        CanManageUsers  = false;
        CanViewAudit    = false;

        using var scope = serviceProvider.CreateScope();
        var permissionSnapshot = scope.ServiceProvider.GetRequiredService<IPermissionSnapshotService>();
        permissionSnapshot.InvalidateCache();

        Navigation.NavigateTo<LoginViewModel>();
    }

    [RelayCommand]
    private void GoToDashboard() => Navigation.NavigateTo<DashboardViewModel>();

    [RelayCommand]
    private void GoToProjects() => Navigation.NavigateTo<ProjectListViewModel>();

    [RelayCommand]
    private void GoToUsers() => Navigation.NavigateTo<UsersViewModel>();

    [RelayCommand]
    private void GoToAudit() => Navigation.NavigateTo<AuditViewModel>();

    [RelayCommand]
    private void GoToSettings() => Navigation.NavigateTo<SettingsViewModel>();

    [RelayCommand]
    private async Task LogoutAsync()
    {
        using var scope  = serviceProvider.CreateScope();
        var authService  = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await authService.LogoutAsync();
        OnLoggedOut();
    }
}
