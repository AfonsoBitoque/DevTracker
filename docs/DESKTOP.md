# DevTracker.Desktop - Análise Técnica

Camada de apresentação. Aplicação Avalonia UI com MVVM, navegação própria, e dialogs modais.

---

## App

### `Program.cs`
**Responsabilidade**: Entry point da aplicação Avalonia.

```csharp
public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
#if DEBUG
        .WithDeveloperTools()
#endif
        .WithInterFont()
        .LogToTrace();
```

### `App.axaml.cs`
**Responsabilidade**: Bootstrap completo da aplicação.

**Passos de inicialização**:

1. **Resolver workspaceRoot** de `config.json` (antes do DI — leitura síncrona)
2. **Construir IServiceCollection**:
   - `AddInfrastructure(appPaths.DatabasePath)`
   - `AddTransient<GitService>()`, `AddTransient<GitHubService>()`
   - `RegisterViewModels(services)`
3. **NavigationService e DialogService** com `Lazy<T>` para evitar ciclos de dependência:
   ```csharp
   var lazyShell = new Lazy<ShellViewModel>(() => _services!.GetRequiredService<ShellViewModel>());
   services.AddSingleton<INavigationService>(sp =>
       new NavigationService(sp, vm => lazyShell.Value.CurrentPage = vm));
   ```
4. **BuildServiceProvider**
5. **EnsureDatabase** — chama `EnsureCreated()`
6. **Criar MainWindow** com `ShellViewModel` como DataContext
7. **Iniciar app** + timer de sessão (30s)

**Timer de sessão** (resolvido após MainWindow atribuído):
```csharp
var sessionTimer = new System.Threading.Timer(_ =>
{
    var session = _services.GetRequiredService<ISessionService>();
    if (session.IsAuthenticated && session.IsExpired())
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        Avalonia.Threading.Dispatcher.UIThread.Post(shell.OnLoggedOut);
    }
}, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
```

**ViewModels registados**:
```csharp
services.AddSingleton<ShellViewModel>();
services.AddTransient<LoginViewModel>();
services.AddTransient<FirstRunSetupViewModel>();
services.AddTransient<ReAuthenticateDialogViewModel>();
services.AddTransient<DashboardViewModel>();
services.AddTransient<ProjectListViewModel>();
services.AddTransient<ProjectDetailViewModel>();
services.AddTransient<CreateProjectDialogViewModel>();
services.AddTransient<ChangeProjectStateDialogViewModel>();
services.AddTransient<WorkItemDetailViewModel>();
services.AddTransient<CreateWorkItemDialogViewModel>();
services.AddTransient<UpdateWorkItemDialogViewModel>();
services.AddTransient<DiffViewModel>();
services.AddTransient<UsersViewModel>();
services.AddTransient<CreateUserDialogViewModel>();
services.AddTransient<AuditViewModel>();
services.AddTransient<SettingsViewModel>();
```

### `MainWindow.axaml.cs`
**Responsabilidade**: Janela principal da aplicação.

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

**UI (AXAML)**: Grid com sidebar (240px) + content area. Toolbar superior vazia (setas back/forward removidas). Bindings para `ShellViewModel`.

### `ViewLocator.cs`
**Responsabilidade**: Resolve Views a partir de ViewModels.

**Algoritmo**:
```csharp
var vmTypeName  = data.GetType().FullName!;  // e.g., "...ViewModels.Auth.LoginViewModel"
var viewTypeName = vmTypeName
    .Replace(".ViewModels.", ".Views.")
    .Replace("ViewModel", "View");            // → "...Views.Auth.LoginView"

var viewType = Type.GetType(viewTypeName);
return (Control?)Activator.CreateInstance(viewType);
```

**Match**: `data is ViewModelBase`

---

## Navigation

### `NavigationService.cs`
**Implementa**: `INavigationService`

**Padrão**: Cria `IServiceScope` por navegação (suporta serviços Scoped nos ViewModels).

**Histórico**: `Stack<(PageViewModelBase vm, IServiceScope scope)>` para histórico de retrocesso.
**Forward History**: `Stack<(PageViewModelBase vm, IServiceScope scope)>` para histórico de avanço.

**Ciclo de vida**:
```csharp
public void NavigateTo<TViewModel>(Action<TViewModel>? configure)
{
    // Limpar histórico de avanço antes de navegar para página nova
    while (_forwardHistory.Count > 0)
    {
        var (_, forwardScope) = _forwardHistory.Pop();
        forwardScope.Dispose();
    }

    var scope = serviceProvider.CreateScope();
    var vm = scope.ServiceProvider.GetRequiredService<TViewModel>();
    configure?.Invoke(vm);
    
    // Notificar desativação e empurrar estado atual
    _ = _current?.OnDeactivatedAsync();
    if (_current is not null && _currentScope is not null)
        _history.Push((_current, _currentScope));
    
    _current = page;
    _currentScope = scope;
    setCurrentPage(page);
    _ = page.OnActivatedAsync();
}
```

**NavigateBack**:
```csharp
public void NavigateBack()
{
    _ = _current?.OnDeactivatedAsync();

    // Guardar no histórico de avanço em vez de descartar
    if (_current is not null && _currentScope is not null)
        _forwardHistory.Push((_current, _currentScope));
    
    (_current, _currentScope) = _history.Pop();
    setCurrentPage(_current);
    _ = _current.OnActivatedAsync();
}
```

### `AvaloniaDialogService.cs`
**Implementa**: `IDialogService`

**Padrão**: `TaskCompletionSource` para awaitar fechamento do dialog.

**ShowDialogAsync<TViewModel>**:
```csharp
using var scope = serviceProvider.CreateScope();
var vm = scope.ServiceProvider.GetRequiredService<TViewModel>();
configure?.Invoke(vm);

var tcs = new TaskCompletionSource<bool>();
var window = new DialogWindow { DataContext = vm };

// Wire no evento CloseRequested do VM
dialogVm.CloseRequested += result => { tcs.TrySetResult(result); window.Close(); };
// Wire no Closed para evitar hang quando utilizador fecha pela X
window.Closed += (_, _) => tcs.TrySetResult(false);

await window.ShowDialog(mainWindowFactory());
return await tcs.Task;
```

**ConfirmAsync / AlertAsync**: Usam `DialogWindow` com `CanResize="True"`, `SizeToContent="WidthAndHeight"`, `MinWidth/MinHeight`. Janela adapta-se ao conteúdo.

---

## ViewModels Base

### `ViewModelBase.cs`
**Herança**: `ObservableObject` (CommunityToolkit.Mvvm)

**Propriedades**:
```csharp
[ObservableProperty] private bool    _isBusy;
[ObservableProperty] private string? _errorMessage;
[ObservableProperty] private string? _successMessage;
```

**Helpers**:
```csharp
protected void ClearMessages()      // Limpa ambas as mensagens
protected void SetError(string)     // Define erro, limpa sucesso
protected void SetSuccess(string)   // Define sucesso, limpa erro
```

**Princípio**: Views são passivas — zero lógica de negócio no code-behind.

### `PageViewModelBase.cs`
**Herança**: `ViewModelBase`

**Ciclo de vida**:
```csharp
public virtual Task OnActivatedAsync(CancellationToken ct = default) => Task.CompletedTask;
public virtual Task OnDeactivatedAsync() => Task.CompletedTask;
```

**Observação**: Carregar permissões (`IPermissionSnapshotService`) e dados iniciais no `OnActivatedAsync`.

### `DialogViewModelBase.cs`
**Herança**: `ViewModelBase`

```csharp
public event Action<bool>? CloseRequested;
protected void Confirm() => CloseRequested?.Invoke(true);
protected void Cancel()  => CloseRequested?.Invoke(false);
```

### `DialogViewModelBase<TResult>.cs`
```csharp
public event Action<TResult?>? CloseWithResult;
protected void ConfirmWithResult(TResult result) => CloseWithResult?.Invoke(result);
```

---

## Shell

### `ShellViewModel.cs`
**Herança**: `ViewModelBase`
**Lifetime**: Singleton

**Responsabilidade**: ViewModel raiz da aplicação.

**Propriedades**:
```csharp
[ObservableProperty] private ViewModelBase? _currentPage;
[ObservableProperty] private bool           _isAuthenticated;
[ObservableProperty] private bool           _canManageUsers;
[ObservableProperty] private bool           _canViewAudit;
```

**Inicialização** (`InitializeAsync` — agora com try/catch):
```csharp
public async Task InitializeAsync()
{
    try
    {
        using var scope = serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        
        if (!await settingsService.IsInitializedAsync())
        {
            Navigation.NavigateTo<FirstRunSetupViewModel>();
            return;
        }
        
        if (!await authService.AnyUsersExistAsync())
        {
            Navigation.NavigateTo<FirstRunSetupViewModel>();
            return;
        }
        
        Navigation.NavigateTo<LoginViewModel>();
    }
    catch (Exception ex)
    {
        SetError($"Erro ao inicializar: {ex.Message}");
        Navigation.NavigateTo<LoginViewModel>();
    }
}
```

**OnAuthenticatedAsync**:
```csharp
public async Task OnAuthenticatedAsync()
{
    IsAuthenticated = true;
    
    using var scope = serviceProvider.CreateScope();
    var permissionSnapshot = scope.ServiceProvider.GetRequiredService<IPermissionSnapshotService>();
    var permissions = await permissionSnapshot.GetCurrentUserPermissionsAsync();
    CanManageUsers = permissions.Contains(Permissions.UserCreate);
    CanViewAudit   = permissions.Contains(Permissions.AuditRead);
    
    Navigation.NavigateTo<DashboardViewModel>();
}
```

**OnLoggedOut** (agora invalida cache de permissões):
```csharp
public void OnLoggedOut()
{
    IsAuthenticated = false;
    CanManageUsers  = false;
    CanViewAudit    = false;
    
    // Invalidar cache de permissões para evitar stale data
    using var scope = serviceProvider.CreateScope();
    var snapshot = scope.ServiceProvider.GetRequiredService<IPermissionSnapshotService>();
    snapshot.InvalidateCache();
    
    Navigation.NavigateTo<LoginViewModel>();
}
```

**Comandos da sidebar**:
```csharp
[RelayCommand] private void GoToDashboard() => Navigation.NavigateTo<DashboardViewModel>();
[RelayCommand] private void GoToProjects()    => Navigation.NavigateTo<ProjectListViewModel>();
[RelayCommand] private void GoToUsers()       => Navigation.NavigateTo<UsersViewModel>();
[RelayCommand] private void GoToAudit()       => Navigation.NavigateTo<AuditViewModel>();
[RelayCommand] private void GoToSettings()     => Navigation.NavigateTo<SettingsViewModel>();
[RelayCommand] private async Task LogoutAsync()
{
    using var scope = serviceProvider.CreateScope();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.LogoutAsync();
    OnLoggedOut();
}
```

**Observação**: Usa `IServiceProvider` para criar scopes temporários — `ShellViewModel` é Singleton e não deve depender diretamente de serviços Scoped (evita captive dependency).

---

## Auth ViewModels

### `LoginViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `IAuthService`, `ShellViewModel`

**Propriedades**:
```csharp
[ObservableProperty] private string _username = string.Empty;
[ObservableProperty] private string _password = string.Empty;
[ObservableProperty] private bool   _isReAuthMode;
```

**Comando LoginAsync** (password limpo em finally):
```csharp
[RelayCommand(CanExecute = nameof(CanLogin))]
private async Task LoginAsync()
{
    IsBusy = true;
    ClearMessages();
    try
    {
        var result = await authService.LoginAsync(Username.Trim(), Password);
        if (!result.Succeeded) { SetError(result.Error!); return; }
        await shell.OnAuthenticatedAsync();
    }
    catch (Exception ex) { SetError($"Erro inesperado: {ex.Message}"); }
    finally
    {
        Password = string.Empty;  // Limpar em ambos os casos (sucesso e falha)
        IsBusy = false;
    }
}
```

**CanExecute**: `CanLogin = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsBusy`

### `FirstRunSetupViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `ISettingsService`, `IWorkspaceService`, `IAuthService`, `IAppPaths`, `ShellViewModel`

**Fluxo**:
1. **Defesa**: `OnActivatedAsync` verifica `AnyUsersExistAsync()` — se existirem utilizadores, redireciona automaticamente para `LoginViewModel`.
2. **Workspace não configurado** (`!appPaths.IsWorkspaceConfigured`):
   - Pedir `WorkspacePath`
   - `EnsureWorkspaceRootAsync` → cria pasta + subpasta `_archive`
   - `SaveAsync` → guarda config.json
   - `RestartApp()` — reinicia o processo (IAppPaths é imutável)
3. **Sem utilizadores**:
   - Pedir `Username` + `Password` + `ConfirmPassword`
   - `SetupFirstOwnerAsync` → cria Owner
   - Navega para Login (sem reiniciar)

### `ReAuthenticateDialogViewModel.cs`
**Herança**: `DialogViewModelBase`
**Dependências**: `IReAuthenticationService`, `ICurrentUserService`

**Fluxo**: Utilizador insere password → `ConfirmPasswordAsync` → fecha com `true`/`false`.

---

## Dashboard

### `DashboardViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependência**: `IDashboardService`

**Propriedades**:
```csharp
[ObservableProperty] private DashboardMetricsDto? _metrics;
[ObservableProperty] private bool _isLoading;
```

**OnActivatedAsync**: Chama `LoadAsync` para carregar métricas.

**LoadAsync**:
```csharp
IsLoading = true;
try
{
    var result = await dashboardService.GetUserMetricsAsync(ct);
    if (result.Succeeded) Metrics = result.Value;
}
finally { IsLoading = false; }
```

---

## Projects ViewModels

### `ProjectListViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `IProjectService`, `IPermissionSnapshotService`, `INavigationService`, `IDialogService`

**Propriedades**:
```csharp
[ObservableProperty] private ObservableCollection<ProjectSummaryDto> _projects = [];
[ObservableProperty] private bool _canCreate;
[ObservableProperty] private bool _canDelete;
```

**OnActivatedAsync**:
```csharp
var permissions = await permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
CanCreate = permissions.Contains(Permissions.ProjectCreate);
CanDelete = permissions.Contains(Permissions.ProjectDelete);
await LoadProjectsAsync(ct);
```

**Comandos**:
- `OpenProject(ProjectSummaryDto)`: `navigation.NavigateTo<ProjectDetailViewModel>(vm => vm.ProjectId = project.Id)`
- `CreateProjectAsync`: `dialogService.ShowDialogAsync<CreateProjectDialogViewModel>()` → reload
- `DeleteProjectAsync`: `ConfirmAsync` → `ReAuthenticateDialog` → `projectService.DeleteAsync` → remove da lista

### `ProjectDetailViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `IProjectService`, `IWorkItemService`, `IPermissionSnapshotService`, `INavigationService`, `IDialogService`, `GitService`, `IWorkspaceService`

**Propriedades**:
```csharp
public Guid ProjectId { get; set; }  // Definido pelo NavigationService
[ObservableProperty] private ProjectDetailDto? _project;
[ObservableProperty] private bool _canUpdate;
[ObservableProperty] private bool _canChangeState;
[ObservableProperty] private bool _canCreateWorkItem;
[ObservableProperty] private ObservableCollection<FileEntryViewModel> _files = [];
[ObservableProperty] private ObservableCollection<WorkItemSummaryDto> _activeItems = [];
[ObservableProperty] private ObservableCollection<WorkItemSummaryDto> _todoItems = [];
[ObservableProperty] private ObservableCollection<WorkItemSummaryDto> _doneItems = [];
```

**Kanban**: `FilterWorkItems` distribui por 3 colunas:
```csharp
ActiveItems = allItems.Where(i => i.Status == InProgress);
TodoItems   = allItems.Where(i => i.Status == Todo || i.Status == Backlog);
DoneItems   = allItems.Where(i => i.Status == Done);
```

**CloneAsync** (clone para temp + copy cross-device safe):
- Clona para `tempPath` (Path.GetTempPath + Guid)
- Usa `workspaceService.CopyDirectoryContentsAsync(tempPath, Project.WorkspacePath)` para copiar (evita cross-device link em Linux)
- Limpa `tempPath` no `finally`

**File Explorer**: `LoadFilesAsync` delega a `IWorkspaceService.ListEntriesAsync`:
```csharp
var result = await workspaceService.ListEntriesAsync(path);
Files = new ObservableCollection<FileEntryViewModel>(
    result.Value!.Select(e => new FileEntryViewModel(e.Name, e.FullPath, e.IsDirectory)));
```

### `CreateProjectDialogViewModel.cs`
**Herança**: `DialogViewModelBase`
**Dependência**: `IProjectService`

**Propriedades**: `Name`, `Description`, `Color` (default `#2563EB`), `GitHubRepositoryUrl`

**Observação**: `WorkspacePath` é enviado como `string.Empty` — o `ProjectService` cria a pasta automaticamente via `WorkspaceService`.

### `ChangeProjectStateDialogViewModel.cs`
**Herança**: `DialogViewModelBase`
**Dependência**: `IProjectService`

**Propriedades**: `ProjectId`, `NewState` (default `InProgress`), `Reason`

**Regra**: `RequiresReason` é `true` quando `NewState` é `Paused` ou `Cancelled`.

---

## WorkItems ViewModels

### `WorkItemDetailViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `IWorkItemService`, `IProjectService`, `GitService`, `IPermissionSnapshotService`, `INavigationService`, `IDialogService`

**Propriedades**:
```csharp
public Guid WorkItemId { get; set; }
[ObservableProperty] private WorkItemDetailDto? _workItem;
[ObservableProperty] private string _newComment = string.Empty;
[ObservableProperty] private bool _canUpdate;
[ObservableProperty] private bool _canDelete;
[ObservableProperty] private bool _canComment;
[ObservableProperty] private bool _canChangeStatus;
[ObservableProperty] private bool _canStartTask;
[ObservableProperty] private bool _canFinishTask;
```

**DeleteAsync** (com re-autenticação):
1. `ConfirmAsync`
2. `ShowDialogAsync<ReAuthenticateDialogViewModel>`
3. `workItemService.DeleteAsync`

**StartTaskAsync** (transaction-like: git primeiro, depois muda estado):
1. Cria branch `feature/task-{Number}-{Title}` (sanitizado pelo GitService)
2. Faz commit snapshot via `gitService`
3. Só depois muda estado para `InProgress`
4. Se git falhar, estado não muda

**FinishTaskAsync** (transaction-like + diff dialog):
1. Obtém diff via `gitService.GetDiffAsync`
2. Mostra diff Meld-style via `ShowDialogAsync<DiffViewModel>` (side-by-side com cores)
3. Commit no branch atual (não cria novo branch)
4. Só depois muda estado para `Done`
5. Se qualquer passo git falhar, estado não muda

### `CreateWorkItemDialogViewModel.cs`
**Herança**: `DialogViewModelBase`
**Dependência**: `IWorkItemService`

**Propriedades**: `Title`, `Description`, `Type`, `Priority`, `Difficulty`, `EstimatedTime`

**Listas de opções**:
```csharp
AvailableTypes: [Task, Feature, Bug, Improvement, Documentation]
AvailablePriorities: [Low, Medium, High, Critical]
AvailableDifficulties: [VeryEasy, Easy, Medium, Hard, VeryHard]
AvailableEstimatedTimes: [LessThan1Hour, OneToTwoHours, HalfDay, OneDay, TwoToThreeDays, MoreThanThreeDays]
```

### `UpdateWorkItemDialogViewModel.cs`
**Herança**: `DialogViewModelBase`
**Dependência**: `IWorkItemService`

**Método**: `LoadAsync` → carrega dados existentes do work item via `GetByIdAsync`.

### `DiffViewModel.cs`
**Herança**: `DialogViewModelBase`

**Modelo**: `DiffLine` com `Type` (`Header`, `Context`, `Removed`, `Added`) + `Text`.
**Parser**: Divide diff unified em `LeftLines` / `RightLines` — removidas vão para esquerda, adicionadas para direita, contexto para ambas.
**Propriedades**: `LeftLines`, `RightLines`, `WorkItemTitle`, `WorkItemDescription`.
**View**: `DiffView.axaml` — duas colunas `ItemsControl` lado a lado (estilo Meld), com cores via `DiffLineBrushConverter`.
**Remoção**: AvaloniaEdit removido do projeto.

---

## Users ViewModels

### `UsersViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `IUserService`, `IPermissionSnapshotService`, `IDialogService`

**Propriedades**:
```csharp
[ObservableProperty] private ObservableCollection<UserSummaryDto> _users = [];
[ObservableProperty] private bool _canCreate;
[ObservableProperty] private bool _canChangeRole;
[ObservableProperty] private bool _canDeactivate;
```

**OnActivatedAsync**: Carrega permissões + lista de utilizadores.

**Comandos**: `RefreshAsync`, `CreateUserAsync`, `DeactivateUserAsync`

**DeactivateUserAsync** (com re-autenticação):
1. `ConfirmAsync`
2. `ShowDialogAsync<ReAuthenticateDialogViewModel>`
3. `userService.DeactivateAsync`

### `CreateUserDialogViewModel.cs`
**Herança**: `DialogViewModelBase`
**Dependência**: `IUserService`

**Propriedades**: `Username`, `Password`, `ConfirmPassword`, `Role`

**Regra**: `AvailableRoles = [Admin, Maintainer, Reader]` — exclui Owner (só via bootstrap).

---

## Audit

### `AuditViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `IAuditService`, `IPermissionSnapshotService`

**Propriedades**:
```csharp
[ObservableProperty] private ObservableCollection<AuditEntryDto> _entries = [];
```

**OnActivatedAsync**:
```csharp
var permissions = await permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
if (!permissions.Contains(Permissions.AuditRead))
{
    SetError("Sem permissão para ver auditoria.");
    return;
}
await LoadAsync(ct);
```

**Limite**: Últimas 100 entradas (`GetRecentAsync(100)`).

---

## Settings

### `SettingsViewModel.cs`
**Herança**: `PageViewModelBase`
**Dependências**: `ISettingsService`

**Propriedades**:
```csharp
[ObservableProperty] private string _gitHubToken = string.Empty;      // Apenas em memória — NUNCA persistido
[ObservableProperty] private string _gitHubUsername = string.Empty;  // Persistido via AppSettings
```

**Persistência**: Delega completamente a `ISettingsService` (config.json).
- `GitHubUsername` → `AppSettings.GitHubUsername` → `SaveAsync`
- `GitHubToken` → **nunca persistido em disco** (in-memory only)

**Load**:
```csharp
public override async Task OnActivatedAsync(CancellationToken ct)
{
    var settings = await settingsService.LoadAsync();
    GitHubUsername = settings.GitHubUsername;
}
```

**Save**:
```csharp
[RelayCommand]
private async Task SaveAsync()
{
    var settings = settingsService.Current;
    settings.GitHubUsername = GitHubUsername;
    var result = await settingsService.SaveAsync(settings);
    if (!result.Succeeded) { SetError(result.Error!); return; }
    SetSuccess("Configurações guardadas com sucesso.");
}
```

**Nota**: O antigo `settings.txt` foi removido. Não existe mais `GetGitHubTokenAsync` helper estático.

---

## Helpers

### `FileEntryViewModel.cs`
**Responsabilidade**: Representa uma entrada no filesystem do projeto.

```csharp
public sealed class FileEntryViewModel(string name, string fullPath, bool isDirectory)
{
    public string Name        { get; } = name;
    public string FullPath    { get; } = fullPath;
    public bool   IsDirectory { get; } = isDirectory;
    public string Icon        => IsDirectory ? "📁" : GetFileIcon(name);
}
```

**Ícones por extensão**:
| Extensões | Ícone |
|-----------|-------|
| .txt, .md, .rst | 📄 |
| .html, .htm, .css, .js, .ts | 🌐 |
| .cs, .py, .java, .go, .rs | 📝 |
| .json, .yaml, .yml, .toml | ⚙️ |
| .png, .jpg, .jpeg, .gif, .svg | 🖼️ |
| .zip, .tar, .gz | 📦 |
| .pdf | 📕 |
| *default* | 📄 |

---

## Padrões Arquiteturais

### Injeção de Dependências
| Lifetime | Serviços |
|----------|----------|
| **Singleton** | ShellViewModel, ISessionService, ICurrentUserService, ICriticalActionTokenService, ISettingsService, IAppPaths |
| **Scoped** | IPermissionService, IPermissionSnapshotService, IAuthService, IProjectService, IWorkItemService, IRepositoryService, IUserService, IAuditService, IDashboardService, IWorkspaceService, IPasswordHasher |
| **Transient** | Todos os ViewModels (exceto ShellViewModel), GitService, GitHubService |

### MVVM
- **Model**: Entidades e DTOs (Core + Application)
- **View**: Ficheiros .axaml (não documentados — zero lógica)
- **ViewModel**: Herda ViewModelBase/PageViewModelBase/DialogViewModelBase
- **Binding**: CommunityToolkit.Mvvm (`ObservableProperty`, `RelayCommand`)

### Navegação
- Toda a navegação passa por `INavigationService`
- Todos os dialogs passam por `IDialogService`
- ViewModels de página são Transient (instância nova por navegação)
- ViewModels de dialog são resolvidos via DI scope isolado
- NavigateTo limpa forward history antes de criar nova página

### Re-autenticação
**Fluxo para operações destrutivas**:
1. `ConfirmAsync("Título", "Mensagem")` → se cancelado, return
2. `ShowDialogAsync<ReAuthenticateDialogViewModel>()` → se falhou, return
3. Executar operação (Delete, Deactivate, etc.)

### Session Lock
- Timer a cada 30s verifica `IsExpired()` / `IsLocked`
- Se expirada: `Dispatcher.UIThread.Post(shell.OnLoggedOut)`
- Redireciona para `LoginViewModel` com `IsReAuthMode=true`

### Autorização UI
- ViewModels carregam permissões **UMA VEZ** no `OnActivatedAsync`
- Propriedades `Can*` ligadas a `RelayCommand(CanExecute = nameof(Can*))`
- Partial methods `OnCan*Changed` notificam `NotifyCanExecuteChanged`
- Sidebar usa `IsVisible` binding para mostrar/esconder itens
