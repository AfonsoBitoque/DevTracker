# DevTracker — Navigation & Dialogs Context

> Ficheiro de contexto: padrão de navegação entre páginas, gestão de dialogs modais e fluxos de autenticação.
> Usar em conjunto com STACK.md, VIEWMODELS.md e AUTH.md.

---

## 1. Problema de navegação em Avalonia

Avalonia não tem um router ou NavigationService nativo equivalente ao de WPF/WinUI.  
A solução adotada no DevTracker é um **NavigationService próprio** baseado em substituição do `CurrentPage` no `ShellViewModel`.

---

## 2. INavigationService

```csharp
// src/DevTracker.Application/Abstractions/Navigation/INavigationService.cs
namespace DevTracker.Application.Abstractions.Navigation;

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo<TViewModel>(Action<TViewModel> configure) where TViewModel : class;
    void NavigateBack();
    bool CanNavigateBack { get; }
}
```

---

## 3. Implementação do NavigationService

```csharp
// src/DevTracker.Desktop/Services/NavigationService.cs
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Desktop.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Action<PageViewModelBase> _setCurrentPage;
    private readonly Stack<PageViewModelBase> _history = new();
    private PageViewModelBase? _current;

    public bool CanNavigateBack => _history.Count > 0;

    public NavigationService(IServiceProvider services, Action<PageViewModelBase> setCurrentPage)
    {
        _services = services;
        _setCurrentPage = setCurrentPage;
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
        => NavigateTo<TViewModel>(_ => { });

    public void NavigateTo<TViewModel>(Action<TViewModel> configure) where TViewModel : class
    {
        var vm = _services.GetRequiredService<TViewModel>();
        configure(vm);

        if (vm is not PageViewModelBase pageVm)
            throw new InvalidOperationException($"{typeof(TViewModel).Name} não herda PageViewModelBase.");

        if (_current is not null)
            _history.Push(_current);

        _current = pageVm;
        _setCurrentPage(pageVm);

        _ = pageVm.OnActivatedAsync();
    }

    public void NavigateBack()
    {
        if (!CanNavigateBack)
            return;

        var previous = _history.Pop();
        _current = previous;
        _setCurrentPage(previous);

        _ = previous.OnActivatedAsync();
    }
}
```

### Registo no DI e ligação ao ShellViewModel

```csharp
// No Program.cs, após construir o ShellViewModel
var shell = services.GetRequiredService<ShellViewModel>();

var navigationService = new NavigationService(
    services,
    vm => shell.CurrentPage = vm);

services.AddSingleton<INavigationService>(navigationService);
```

---

## 4. Dialogs modais

Avalonia não tem um serviço de diálogo nativo. O DevTracker usa um padrão de `IDialogService` para manter os ViewModels testáveis.

### IDialogService

```csharp
// src/DevTracker.Application/Abstractions/Navigation/IDialogService.cs
namespace DevTracker.Application.Abstractions.Navigation;

public interface IDialogService
{
    /// <summary>
    /// Abre um dialog modal e aguarda o resultado (true = confirmado, false = cancelado).
    /// </summary>
    Task<bool> ShowDialogAsync<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : class;

    /// <summary>
    /// Abre um dialog modal e devolve o resultado tipado.
    /// </summary>
    Task<TResult?> ShowDialogAsync<TViewModel, TResult>(Action<TViewModel>? configure = null)
        where TViewModel : class;

    /// <summary>
    /// Abre um dialog de confirmação simples.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>
    /// Mostra um alerta informativo.
    /// </summary>
    Task AlertAsync(string title, string message);
}
```

### Implementação em Avalonia

```csharp
// src/DevTracker.Desktop/Services/AvaloniaDialogService.cs
using Avalonia.Controls;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Desktop.ViewModels.Base;
using DevTracker.Desktop.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop.Services;

public sealed class AvaloniaDialogService : IDialogService
{
    private readonly IServiceProvider _services;
    private readonly Func<Window> _getMainWindow;

    public AvaloniaDialogService(IServiceProvider services, Func<Window> getMainWindow)
    {
        _services = services;
        _getMainWindow = getMainWindow;
    }

    public async Task<bool> ShowDialogAsync<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : class
    {
        var vm = _services.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        if (vm is not DialogViewModelBase dialogVm)
            throw new InvalidOperationException($"{typeof(TViewModel).Name} não herda DialogViewModelBase.");

        var result = false;
        var tcs = new TaskCompletionSource<bool>();

        dialogVm.CloseRequested += confirmed =>
        {
            result = confirmed;
            tcs.SetResult(confirmed);
        };

        var window = new DialogWindow { DataContext = dialogVm };
        _getMainWindow().ShowDialog(window);

        await tcs.Task;
        return result;
    }

    public async Task<TResult?> ShowDialogAsync<TViewModel, TResult>(Action<TViewModel>? configure = null)
        where TViewModel : class
    {
        var vm = _services.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        if (vm is not DialogViewModelBase<TResult> typedDialogVm)
            throw new InvalidOperationException($"{typeof(TViewModel).Name} não herda DialogViewModelBase<TResult>.");

        var tcs = new TaskCompletionSource<TResult?>();
        typedDialogVm.CloseWithResult += result => tcs.SetResult(result);

        var window = new DialogWindow { DataContext = typedDialogVm };
        _getMainWindow().ShowDialog(window);

        return await tcs.Task;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var vm = new ConfirmDialogViewModel(title, message);
        var tcs = new TaskCompletionSource<bool>();
        vm.CloseRequested += result => tcs.SetResult(result);

        var window = new ConfirmDialogWindow { DataContext = vm };
        _getMainWindow().ShowDialog(window);

        return await tcs.Task;
    }

    public async Task AlertAsync(string title, string message)
    {
        var vm = new AlertDialogViewModel(title, message);
        var tcs = new TaskCompletionSource<bool>();
        vm.CloseRequested += _ => tcs.SetResult(true);

        var window = new AlertDialogWindow { DataContext = vm };
        _getMainWindow().ShowDialog(window);

        await tcs.Task;
    }
}
```

---

## 5. DialogViewModelBase com resultado tipado

Para dialogs que devolvem dados (ex: formulários):

```csharp
// src/DevTracker.Desktop/ViewModels/Base/DialogViewModelBase.cs (revisão)
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

public abstract partial class DialogViewModelBase<TResult> : ViewModelBase
{
    public event Action<TResult?>? CloseWithResult;

    [RelayCommand]
    protected virtual void Cancel() => CloseWithResult?.Invoke(default);

    protected void ConfirmWithResult(TResult result) => CloseWithResult?.Invoke(result);
}
```

---

## 6. Fluxo de Re-autenticação

O padrão correto para operações destrutivas que exigem confirmação de password:

```csharp
// Exemplo de uso num ViewModel de projeto
[RelayCommand]
private async Task DeleteProjectAsync(ProjectSummaryDto project)
{
    // 1. Confirmação visual simples
    var confirmed = await _dialogService.ConfirmAsync(
        "Apagar projeto",
        $"Tem a certeza que quer apagar '{project.Name}'? Esta ação é irreversível.");

    if (!confirmed) return;

    // 2. Re-autenticação
    var reAuthPassed = await _dialogService.ShowDialogAsync<ReAuthenticateDialogViewModel>();
    if (!reAuthPassed) return;

    // 3. Executar a operação destrutiva
    IsBusy = true;
    try
    {
        var result = await _projectService.DeleteAsync(project.Id);

        if (!result.Succeeded)
            SetError(result.Error!);
        else
            await OnActivatedAsync(); // Recarregar lista
    }
    finally
    {
        IsBusy = false;
    }
}
```

---

## 7. Fluxos de navegação principais

### Fluxo de arranque

```
App.OnFrameworkInitializationCompleted()
    └── ShellViewModel.InitializeAsync()
            ├── IsInitializedAsync() == false (sem workspace)
            │       └── NavigateTo<FirstRunSetupViewModel>()
            │               └── Após setup: reiniciar app
            │
            └── IsInitializedAsync() == true
                    ├── AnyUsersExistAsync() == false
                    │       └── NavigateTo<FirstRunSetupViewModel>() (criar primeiro owner)
                    │               └── Após setup: NavigateTo<ProjectListViewModel>()
                    │
                    └── AnyUsersExistAsync() == true
                            └── NavigateTo<LoginViewModel>()
                                    └── Login bem-sucedido: NavigateTo<ProjectListViewModel>()
```

### Fluxo pós-login

```
ProjectListViewModel (página inicial)
    ├── Clique em projeto → NavigateTo<ProjectDetailViewModel>(vm => vm.ProjectId = id)
    │       └── Clique em work item → NavigateTo<WorkItemDetailViewModel>(vm => vm.WorkItemId = id)
    │
    ├── Sidebar: Utilizadores → NavigateTo<UserListViewModel>()
    ├── Sidebar: Audit → NavigateTo<AuditLogViewModel>()
    └── Sidebar: Settings → NavigateTo<SettingsViewModel>()
```

### Fluxo de logout

```
ShellViewModel.LogoutCommand
    └── IAuthService.LogoutAsync()
            └── NavigateTo<LoginViewModel>()
                    └── Limpar history de navegação
```

---

## 8. Sidebar / Menu de navegação

A sidebar é controlada pelo `ShellViewModel` e só mostra itens acessíveis ao utilizador autenticado.

```csharp
// src/DevTracker.Desktop/ViewModels/Shell/ShellViewModel.cs (extensão)
[ObservableProperty]
private bool _canManageUsers;

[ObservableProperty]
private bool _canViewAudit;

public async Task RefreshPermissionsAsync(CancellationToken ct = default)
{
    if (!_currentUser.IsAuthenticated) return;

    var permissions = await _permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
    CanManageUsers = permissions.Contains(Permissions.UserCreate);
    CanViewAudit   = permissions.Contains(Permissions.AuditRead);
}
```

```xml
<!-- Sidebar em MainWindow.axaml -->
<StackPanel>
    <Button Content="Projetos" Command="{Binding NavigateToProjectsCommand}" />
    <Button Content="Utilizadores"
            Command="{Binding NavigateToUsersCommand}"
            IsVisible="{Binding CanManageUsers}" />
    <Button Content="Audit Log"
            Command="{Binding NavigateToAuditCommand}"
            IsVisible="{Binding CanViewAudit}" />
    <Button Content="Definições" Command="{Binding NavigateToSettingsCommand}" />
</StackPanel>
```

---

## 9. Session lock — redirecionar automaticamente

Quando a sessão fica bloqueada por inatividade, a app deve voltar ao ecrã de login.

```csharp
// Verificação de sessão — chamada periodicamente (ex: a cada 30s via DispatcherTimer)
private async void OnSessionCheckTimer(object? sender, EventArgs e)
{
    if (_sessionService.IsExpired())
    {
        await _authService.LogoutAsync();
        _navigationService.NavigateTo<LoginViewModel>();
        return;
    }

    if (_sessionService.IsLocked)
    {
        _navigationService.NavigateTo<LoginViewModel>(vm =>
            vm.IsReAuthMode = true);
    }
}
```

---

## 10. Registo de serviços

```csharp
// src/DevTracker.Desktop/Program.cs
// Após construir o ShellViewModel:
var shell = services.GetRequiredService<ShellViewModel>();

services.AddSingleton<INavigationService>(sp => new NavigationService(
    sp,
    vm => shell.CurrentPage = vm));

services.AddSingleton<IDialogService>(sp => new AvaloniaDialogService(
    sp,
    () => (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!
              .MainWindow!));
```

---

## 11. Regras finais

1. Toda a navegação passa por `INavigationService` — nunca criar Views diretamente nos ViewModels.
2. Dialogs modais passam sempre por `IDialogService` — nunca instanciar `Window` nos ViewModels.
3. Re-autenticação acontece **antes** de operações destrutivas, nunca dentro do serviço de domínio.
4. `NavigateBack()` limpa o estado do ViewModel anterior via `OnDeactivatedAsync()`.
5. A sidebar adapta-se às permissões do utilizador autenticado, mas a segurança real está nos serviços.
6. Session lock redireciona automaticamente para login sem perder dados não guardados (exibir aviso ao utilizador).
7. Após logout, o histórico de navegação é limpo.
