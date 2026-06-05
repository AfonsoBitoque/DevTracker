using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Desktop.Navigation;
using DevTracker.Desktop.ViewModels;
using DevTracker.Desktop.ViewModels.Audit;
using DevTracker.Desktop.ViewModels.Auth;
using DevTracker.Desktop.ViewModels.Projects;
using DevTracker.Desktop.ViewModels.Shell;
using DevTracker.Desktop.ViewModels.Users;
using DevTracker.Desktop.ViewModels.WorkItems;
using DevTracker.Infrastructure;
using DevTracker.Infrastructure.IO;
using DevTracker.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop;

public partial class App : Avalonia.Application
{
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        // 1. Resolver o workspace antes do DI (leitura síncrona de config.json)
        var workspaceRoot = AppPaths.ReadWorkspaceRootFromConfig();
        var appPaths      = new AppPaths(workspaceRoot);

        // 2. Configurar serviços
        var services = new ServiceCollection();
        services.AddSingleton<DevTracker.Application.Abstractions.IO.IAppPaths>(appPaths);
        services.AddInfrastructure(appPaths.DatabasePath, appPaths);
        services.AddHttpClient();
        services.AddTransient<IGitService, GitService>();
        services.AddTransient<IGitHubService, GitHubService>();
        RegisterViewModels(services);

        // NavigationService e DialogService dependem do ShellViewModel (Singleton),
        // mas são registados antes de BuildServiceProvider via Lazy<T> para evitar ciclos.
        var lazyShell  = new Lazy<ShellViewModel>(() => _services!.GetRequiredService<ShellViewModel>());
        var lazyWindow = new Lazy<MainWindow>(() => (MainWindow)((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!).MainWindow!);

        services.AddSingleton<INavigationService>(sp =>
            new NavigationService(sp, vm => lazyShell.Value.CurrentPage = vm));

        services.AddSingleton<IDialogService>(sp =>
            new AvaloniaDialogService(sp, () => lazyWindow.Value));

        services.AddSingleton<IAppNotificationService>(sp =>
            new AvaloniaNotificationService(() => lazyWindow.Value));

        _services = services.BuildServiceProvider();

        // 3. Garantir que o schema da BD existe
        DependencyInjection.EnsureDatabase(_services);

        // 4. Create janela principal
        var shell      = _services.GetRequiredService<ShellViewModel>();
        var mainWindow = new MainWindow { DataContext = shell };

        // 5. Ligar NavigationService ao ShellViewModel (via INavigationService do container)
        shell.Navigation = _services.GetRequiredService<INavigationService>();

        // 6. Iniciar a app
        desktop.MainWindow = mainWindow;
        _ = shell.InitializeAsync();

        // 7. Timer de verificação de sessão (30s)
        // Resolve ShellViewModel dentro do callback para evitar capturar a referência antes da janela estar pronta
        var sessionTimer = new System.Threading.Timer(_ =>
        {
            var session = _services.GetRequiredService<ISessionService>();
            if (session.IsAuthenticated && session.IsExpired())
            {
                var shell = _services.GetRequiredService<ShellViewModel>();
                Avalonia.Threading.Dispatcher.UIThread.Post(shell.OnLoggedOut);
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        desktop.Exit += (_, _) => sessionTimer.Dispose();

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        // Singleton — estado global
        services.AddSingleton<ShellViewModel>();

        // Transient — instância nova por navegação
        services.AddTransient<LoginViewModel>();
        services.AddTransient<FirstRunSetupViewModel>();
        services.AddTransient<ReAuthenticateDialogViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProjectListViewModel>();
        services.AddTransient<ProjectDetailViewModel>();
        services.AddTransient<CreateProjectDialogViewModel>();
        services.AddTransient<EditProjectDialogViewModel>();
        services.AddTransient<ChangeProjectStateDialogViewModel>();
        services.AddTransient<WorkItemDetailViewModel>();
        services.AddTransient<CreateWorkItemDialogViewModel>();
        services.AddTransient<UpdateWorkItemDialogViewModel>();
        services.AddTransient<ManageLabelsDialogViewModel>();
        services.AddTransient<DiffViewModel>();
        services.AddTransient<PauseTaskDialogViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<CreateUserDialogViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}