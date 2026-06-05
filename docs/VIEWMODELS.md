# DevTracker — ViewModels & UI Architecture Context

> Ficheiro de contexto: padrões de ViewModel, navegação, injeção de dependências e integração com Avalonia UI.
> Usar em conjunto com STACK.md, PERMISSIONS.md, AUTH.md e NAVIGATION.md.

---

## 1. Princípios MVVM no DevTracker

- **Views são passivas.** Zero lógica de negócio em code-behind.
- **ViewModels são os controladores de estado da UI.** Toda a orquestração, validação de UX e chamada a serviços vive aqui.
- **Serviços são a única fonte de verdade.** VMs delegam tudo para a camada de aplicação.
- **Sem `static` no acesso a serviços.** Tudo injetado via construtor.
- **CommunityToolkit.Mvvm** é o único gerador de boilerplate permitido (`[ObservableProperty]`, `[RelayCommand]`).

---

## 2. Base ViewModels

### ViewModelBase

```csharp
// src/DevTracker.Desktop/ViewModels/Base/ViewModelBase.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevTracker.Desktop.ViewModels.Base;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    protected void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }

    protected void SetError(string message)
    {
        ErrorMessage = message;
        SuccessMessage = null;
    }

    protected void SetSuccess(string message)
    {
        SuccessMessage = message;
        ErrorMessage = null;
    }
}
```

### PageViewModelBase

Para ViewModels que representam "páginas" navegáveis com ciclo de vida.

```csharp
// src/DevTracker.Desktop/ViewModels/Base/PageViewModelBase.cs
namespace DevTracker.Desktop.ViewModels.Base;

public abstract partial class PageViewModelBase : ViewModelBase
{
    /// <summary>
    /// Chamado quando a view é ativada/navegada.
    /// Override para carregar dados iniciais.
    /// </summary>
    public virtual Task OnActivatedAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Chamado quando a view é desativada/abandonada.
    /// Override para limpar recursos ou cancelar operações.
    /// </summary>
    public virtual Task OnDeactivatedAsync()
        => Task.CompletedTask;
}
```

### DialogViewModelBase

Para ViewModels de janelas modais/dialogs.

```csharp
// src/DevTracker.Desktop/ViewModels/Base/DialogViewModelBase.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevTracker.Desktop.ViewModels.Base;

public abstract partial class DialogViewModelBase : ViewModelBase
{
    public event Action<bool>? CloseRequested;

    [RelayCommand]
    protected virtual void Cancel() => CloseRequested?.Invoke(false);

    protected void Confirm() => CloseRequested?.Invoke(true);
}
```

---

## 3. Estrutura de ViewModels

```
src/DevTracker.Desktop/ViewModels/
├── Base/
│   ├── ViewModelBase.cs
│   ├── PageViewModelBase.cs
│   └── DialogViewModelBase.cs
├── Shell/
│   └── ShellViewModel.cs          // VM raiz: barra lateral, área de conteúdo
├── Auth/
│   ├── LoginViewModel.cs
│   ├── FirstRunSetupViewModel.cs
│   └── ReAuthenticateDialogViewModel.cs
├── Projects/
│   ├── ProjectListViewModel.cs
│   ├── ProjectDetailViewModel.cs
│   └── CreateProjectDialogViewModel.cs
├── WorkItems/
│   ├── WorkItemListViewModel.cs
│   ├── WorkItemDetailViewModel.cs
│   └── CreateWorkItemDialogViewModel.cs
├── Repositories/
│   ├── RepositoryListViewModel.cs
│   └── CreateRepositoryDialogViewModel.cs
├── Users/
│   ├── UserListViewModel.cs
│   └── CreateUserDialogViewModel.cs
├── Audit/
│   └── AuditLogViewModel.cs
└── Settings/
    └── SettingsViewModel.cs
```

---

## 4. Injeção de Dependências em Avalonia

### ViewLocator

Avalonia usa um `IDataTemplate` como ViewLocator para ligar automaticamente VMs a Views.

```csharp
// src/DevTracker.Desktop/ViewLocator.cs
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        var name = data.GetType().FullName!.Replace(
            "ViewModels", "Views",
            StringComparison.Ordinal);

        var type = Type.GetType(name);

        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = $"View não encontrada: {name}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
```

### Registo de convenção de nomes

- VM: `DevTracker.Desktop.ViewModels.Auth.LoginViewModel`
- View: `DevTracker.Desktop.Views.Auth.LoginView`

A convenção é **substituir `ViewModels` por `Views`** no namespace completo.

### Registo de serviços e VMs no Program.cs

```csharp
// src/DevTracker.Desktop/Program.cs
using Avalonia;
using DevTracker.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        var dbPath = AppPaths.ResolveDbPath();
        services
            .AddInfrastructure(dbPath)
            .AddSecurity()
            .AddAuthorization()
            .AddFileSystem(AppPaths.ResolveWorkspaceRoot())
            .AddApplicationServices();

        // ViewModels — Transient para páginas, Singleton para Shell
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<FirstRunSetupViewModel>();
        services.AddTransient<ProjectListViewModel>();
        services.AddTransient<ProjectDetailViewModel>();
        services.AddTransient<WorkItemListViewModel>();
        services.AddTransient<WorkItemDetailViewModel>();
        services.AddTransient<UserListViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Dialogs — Transient sempre
        services.AddTransient<ReAuthenticateDialogViewModel>();
        services.AddTransient<CreateProjectDialogViewModel>();
        services.AddTransient<CreateWorkItemDialogViewModel>();
        services.AddTransient<CreateRepositoryDialogViewModel>();
        services.AddTransient<CreateUserDialogViewModel>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

### Resolução de VMs na App

```csharp
// src/DevTracker.Desktop/App.axaml.cs
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = Program.Services.GetRequiredService<ShellViewModel>();
        desktop.MainWindow = new MainWindow { DataContext = shell };
    }

    base.OnFrameworkInitializationCompleted();
}
```

---

## 5. ShellViewModel

O VM raiz controla qual "página" está visível e o estado global de autenticação.

```csharp
// src/DevTracker.Desktop/ViewModels/Shell/ShellViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Desktop.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop.ViewModels.Shell;

public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthService _authService;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private PageViewModelBase? _currentPage;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _username = string.Empty;

    public ShellViewModel(
        ICurrentUserService currentUser,
        IAuthService authService,
        IServiceProvider services)
    {
        _currentUser = currentUser;
        _authService = authService;
        _services = services;
    }

    public async Task InitializeAsync()
    {
        var hasUsers = await _authService.AnyUsersExistAsync();

        if (!hasUsers)
            NavigateTo<FirstRunSetupViewModel>();
        else
            NavigateTo<LoginViewModel>();
    }

    public void NavigateTo<TViewModel>() where TViewModel : PageViewModelBase
    {
        var vm = _services.GetRequiredService<TViewModel>();
        CurrentPage = vm;
        _ = vm.OnActivatedAsync();
    }

    public void OnLoginSuccess()
    {
        IsAuthenticated = true;
        Username = _currentUser.Username ?? string.Empty;
        NavigateTo<ProjectListViewModel>();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        IsAuthenticated = false;
        Username = string.Empty;
        NavigateTo<LoginViewModel>();
    }
}
```

---

## 6. Padrão de ViewModel de Página — Exemplo: ProjectListViewModel

```csharp
// src/DevTracker.Desktop/ViewModels/Projects/ProjectListViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Desktop.ViewModels.Base;
using System.Collections.ObjectModel;

namespace DevTracker.Desktop.ViewModels.Projects;

public sealed partial class ProjectListViewModel : PageViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IPermissionSnapshotService _permissionSnapshot;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty]
    private ObservableCollection<ProjectSummaryDto> _projects = [];

    [ObservableProperty]
    private ProjectSummaryDto? _selectedProject;

    [ObservableProperty]
    private bool _canCreateProject;

    [ObservableProperty]
    private bool _canDeleteProject;

    public ProjectListViewModel(
        IProjectService projectService,
        IPermissionSnapshotService permissionSnapshot,
        ICurrentUserService currentUser)
    {
        _projectService = projectService;
        _permissionSnapshot = permissionSnapshot;
        _currentUser = currentUser;
    }

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ClearMessages();

        try
        {
            var permissions = await _permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
            CanCreateProject = permissions.Contains(Security.Permissions.ProjectCreate);
            CanDeleteProject = permissions.Contains(Security.Permissions.ProjectDelete);

            var result = await _projectService.GetAllAsync(ct);

            if (result.Succeeded)
            {
                Projects = new ObservableCollection<ProjectSummaryDto>(result.Value!);
            }
            else
            {
                SetError(result.Error!);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private async Task CreateProjectAsync()
    {
        // Abrir diálogo — ver NAVIGATION.md para padrão de dialogs
    }

    [RelayCommand]
    private void SelectProject(ProjectSummaryDto project)
    {
        SelectedProject = project;
        // Navegar para ProjectDetailViewModel
    }
}
```

---

## 7. Padrão de Dialog ViewModel — Exemplo: ReAuthenticateDialogViewModel

```csharp
// src/DevTracker.Desktop/ViewModels/Auth/ReAuthenticateDialogViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Auth;

public sealed partial class ReAuthenticateDialogViewModel : DialogViewModelBase
{
    private readonly IReAuthenticationService _reAuth;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _password = string.Empty;

    public ReAuthenticateDialogViewModel(
        IReAuthenticationService reAuth,
        ICurrentUserService currentUser)
    {
        _reAuth = reAuth;
        _currentUser = currentUser;
    }

    private bool CanConfirm => !string.IsNullOrWhiteSpace(Password);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (!_currentUser.UserId.HasValue)
        {
            SetError("Utilizador não autenticado.");
            return;
        }

        IsBusy = true;
        ClearMessages();

        try
        {
            var result = await _reAuth.ConfirmPasswordAsync(_currentUser.UserId.Value, Password);

            if (result.Succeeded)
            {
                Confirm();
            }
            else
            {
                SetError(result.Error!);
                Password = string.Empty;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

---

## 8. Permissões nos ViewModels

### Regra

Nunca fazer `if (role == Owner)` nos ViewModels. Usar sempre `IPermissionSnapshotService`.

### Padrão recomendado

```csharp
// No OnActivatedAsync de qualquer PageViewModel que precise de adaptar UI
var permissions = await _permissionSnapshot.GetCurrentUserPermissionsAsync(ct);

CanCreate  = permissions.Contains(Permissions.ProjectCreate);
CanDelete  = permissions.Contains(Permissions.ProjectDelete);
CanArchive = permissions.Contains(Permissions.ProjectArchive);
```

### Propriedades de permissão como `bool` observáveis

- Ligar `[RelayCommand(CanExecute = nameof(CanCreate))]` para ativar/desativar comandos automaticamente
- Ligar `IsVisible="{Binding CanDelete}"` nas Views para esconder botões destrutivos

### Nota crítica

Estas propriedades são **apenas UX**. A decisão real acontece nos serviços de aplicação. A UI adapta-se por conveniência, não por segurança.

---

## 9. Tratamento de erros assíncrono

### Padrão de execução segura

Todos os comandos async devem seguir este padrão:

```csharp
[RelayCommand]
private async Task DoSomethingAsync()
{
    IsBusy = true;
    ClearMessages();

    try
    {
        var result = await _service.DoSomethingAsync();

        if (!result.Succeeded)
        {
            SetError(result.Error!);
            return;
        }

        // sucesso
        SetSuccess("Operação concluída.");
    }
    catch (AuthorizationException ex)
    {
        SetError($"Sem permissão: {ex.Message}");
    }
    catch (Exception ex)
    {
        SetError($"Erro inesperado: {ex.Message}");
        // Log via Serilog — nunca expor stack trace diretamente ao utilizador
    }
    finally
    {
        IsBusy = false;
    }
}
```

### Regras

- `IsBusy = true` antes de qualquer operação async
- `IsBusy = false` sempre no `finally`
- Erros de autorização (`AuthorizationException`) têm mensagem específica
- Exceções não esperadas: logar, mostrar mensagem genérica ao utilizador
- Nunca expor stack traces na UI

---

## 10. IsBusy e loading state nas Views

### Binding recomendado em AXAML

```xml
<!-- Overlay de loading -->
<Panel>
    <ContentControl Content="{Binding CurrentContent}" />

    <Border IsVisible="{Binding IsBusy}"
            Background="#80000000">
        <ProgressBar IsIndeterminate="True"
                     HorizontalAlignment="Center"
                     VerticalAlignment="Center" />
    </Border>
</Panel>

<!-- Mensagem de erro -->
<TextBlock Text="{Binding ErrorMessage}"
           IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
           Foreground="Red" />
```

---

## 11. Comunicação entre ViewModels

### Não usar eventos estáticos nem Messenger por padrão

Para casos simples, passar callbacks/actions:

```csharp
// O VM pai passa uma ação ao dialog VM
var dialogVm = _services.GetRequiredService<CreateProjectDialogViewModel>();
dialogVm.OnProjectCreated = async (projectId) =>
{
    await OnActivatedAsync(); // Recarregar lista
};
```

### Para cenários mais complexos — WeakReferenceMessenger (CommunityToolkit)

```csharp
// Publicar
WeakReferenceMessenger.Default.Send(new ProjectCreatedMessage(projectId));

// Subscrever (no VM que precisa de reagir)
WeakReferenceMessenger.Default.Register<ProjectCreatedMessage>(this, (r, m) =>
{
    _ = OnActivatedAsync();
});
```

Usar apenas quando a comunicação é genuinamente entre VMs não relacionados hierarquicamente.

---

## 12. Design de Views — Convenções AXAML

### Estrutura base de uma View de página

```xml
<!-- src/DevTracker.Desktop/Views/Projects/ProjectListView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:DevTracker.Desktop.ViewModels.Projects"
             x:Class="DevTracker.Desktop.Views.Projects.ProjectListView"
             x:DataType="vm:ProjectListViewModel">

    <Grid RowDefinitions="Auto,*,Auto">

        <!-- Header -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="16">
            <TextBlock Text="Projetos" Classes="title" />
            <Button Content="Novo Projeto"
                    Command="{Binding CreateProjectCommand}"
                    IsVisible="{Binding CanCreateProject}"
                    Margin="16,0,0,0" />
        </StackPanel>

        <!-- Lista -->
        <ListBox Grid.Row="1"
                 ItemsSource="{Binding Projects}"
                 SelectedItem="{Binding SelectedProject}" />

        <!-- Mensagem de erro -->
        <TextBlock Grid.Row="2"
                   Text="{Binding ErrorMessage}"
                   IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
                   Foreground="Red"
                   Margin="16" />
    </Grid>

</UserControl>
```

### Regras AXAML

- `x:DataType` obrigatório para compiled bindings (melhor performance, erros em tempo de compilação)
- Nunca usar `FindAncestor` ou bindings relativos a outros elementos — preferir binding ao VM
- Sem code-behind exceto `InitializeComponent()`

---

## 13. Ciclo de vida da App

```
App.OnFrameworkInitializationCompleted()
    └── Cria MainWindow com ShellViewModel
            └── ShellViewModel.InitializeAsync()
                    ├── AnyUsersExistAsync() == false → FirstRunSetupView
                    └── AnyUsersExistAsync() == true  → LoginView
                            └── Login com sucesso → ProjectListView
```

---

## 14. Dependency Injection — Lifetimes

| Tipo | Lifetime | Razão |
|---|---|---|
| `ShellViewModel` | Singleton | Estado global da app |
| Page ViewModels | Transient | Recriados a cada navegação para estado fresco |
| Dialog ViewModels | Transient | Vida útil de um diálogo |
| `ICurrentUserService` | Singleton | Sessão global |
| `ISessionService` | Singleton | Sessão global |
| Serviços de aplicação | Scoped | Por operação/scope |

---

## 15. Regras finais

1. Zero lógica de negócio nas Views.
2. Zero `System.IO` nos ViewModels — delegar a serviços.
3. Permissões sempre via `IPermissionSnapshotService`, nunca via comparação de `Role` direta.
4. Todo o comando async segue o padrão `IsBusy → try/catch/finally → IsBusy = false`.
5. `x:DataType` obrigatório em todas as Views para compiled bindings.
6. VMs de página herdam `PageViewModelBase`; VMs de dialog herdam `DialogViewModelBase`.
7. Comunicação entre VMs preferencialmente via callbacks; `WeakReferenceMessenger` apenas quando necessário.
