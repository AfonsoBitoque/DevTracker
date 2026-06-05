# DevTracker — Settings & Configuration Context

> Ficheiro de contexto: configuração persistida, ISettingsService, bootstrap do workspace e gestão de preferências.
> Usar em conjunto com STACK.md, WORKSPACE.md e AUTH.md.

---

## 1. Objetivo

A app precisa de persistir um conjunto de configurações entre sessões:
- Caminho do workspace escolhido pelo utilizador na primeira execução
- Timeout de sessão por inatividade (configurável)
- Preferências de UI

Estas configurações **não vivem na BD SQLite** — vivem num ficheiro `config.json` separado, em `LocalApplicationData/DevTracker/config.json`.

---

## 2. Modelo de configuração

```csharp
// src/DevTracker.Application/Settings/AppSettings.cs
namespace DevTracker.Application.Settings;

public sealed class AppSettings
{
    /// <summary>Caminho absoluto para a pasta raiz do workspace.</summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    /// <summary>Minutos de inatividade até lock automático da sessão. Default: 15.</summary>
    public int SessionIdleTimeoutMinutes { get; set; } = 15;

    /// <summary>Horas até expiração dura da sessão. Default: 8.</summary>
    public int SessionMaxDurationHours { get; set; } = 8;

    /// <summary>Versão do schema de settings, para migrações futuras.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Username do GitHub (não-sensível, pode ser persistido).</summary>
    public string GitHubUsername { get; set; } = string.Empty;

    /// <summary>Preferências de UI.</summary>
    public UiPreferences Ui { get; set; } = new();
}
```

```csharp
// src/DevTracker.Application/Settings/UiPreferences.cs
namespace DevTracker.Application.Settings;

public sealed class UiPreferences
{
    /// <summary>Tema visual: "Light", "Dark", "System".</summary>
    public string Theme { get; set; } = "System";

    /// <summary>Língua da interface. Default: "pt-PT".</summary>
    public string Language { get; set; } = "pt-PT";

    /// <summary>Largura da barra lateral em pixels.</summary>
    public double SidebarWidth { get; set; } = 240;
}
```

---

## 3. Interface do serviço

```csharp
// src/DevTracker.Application/Abstractions/Settings/ISettingsService.cs
using DevTracker.Application.Common;
using DevTracker.Application.Settings;

namespace DevTracker.Application.Abstractions.Settings;

public interface ISettingsService
{
    /// <summary>Carrega as settings do ficheiro. Retorna defaults se não existir.</summary>
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    /// <summary>Persiste as settings no ficheiro.</summary>
    Task<Result> SaveAsync(AppSettings settings, CancellationToken ct = default);

    /// <summary>Verifica se as settings já foram inicializadas (workspace definido).</summary>
    Task<bool> IsInitializedAsync(CancellationToken ct = default);

    /// <summary>Acesso síncrono às settings atualmente carregadas em memória (após LoadAsync).</summary>
    AppSettings Current { get; }
}
```

---

## 4. Implementação

```csharp
// src/DevTracker.Infrastructure/Settings/SettingsService.cs
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Common;
using DevTracker.Application.Settings;
using System.Text.Json;

namespace DevTracker.Infrastructure.Settings;

public sealed class SettingsService(IAppPaths appPaths) : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private AppSettings _current = new();

    public AppSettings Current => _current;

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        var path = appPaths.ConfigPath;

        if (!File.Exists(path))
        {
            _current = new AppSettings();
            return _current;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            _current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Ficheiro corrompido — usar defaults e não bloquear o arranque
            _current = new AppSettings();
        }

        return _current;
    }

    public async Task<Result> SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(appPaths.ConfigPath)!;
            Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(appPaths.ConfigPath, json, ct);

            _current = settings;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Erro ao guardar configuração: {ex.Message}");
        }
    }

    public async Task<bool> IsInitializedAsync(CancellationToken ct = default)
    {
        var settings = await LoadAsync(ct);
        return !string.IsNullOrWhiteSpace(settings.WorkspaceRoot)
               && Directory.Exists(settings.WorkspaceRoot);
    }
}
```

---

## 5. Bootstrap do WorkspaceRoot — fluxo completo

Este é o fluxo crítico que liga `ISettingsService`, `IWorkspaceService` e o primeiro arranque da app.

### Sequência de arranque em Program.cs

```csharp
// src/DevTracker.Desktop/Program.cs — InitializeAsync (trecho)

// 1. Construir IAppPaths com caminho temporário (sem workspace ainda)
var tempPaths = new AppPaths(workspaceRoot: string.Empty);
var settingsService = new SettingsService(tempPaths);

// 2. Carregar settings
var settings = await settingsService.LoadAsync();

// 3. Verificar se o workspace já foi definido
string resolvedWorkspace;

if (string.IsNullOrWhiteSpace(settings.WorkspaceRoot) || !Directory.Exists(settings.WorkspaceRoot))
{
    // Primeira execução: navegar para FirstRunSetupView (UI define workspace)
    resolvedWorkspace = string.Empty;
}
else
{
    resolvedWorkspace = settings.WorkspaceRoot;
}

// 4. Construir IAppPaths final com o workspace real
var appPaths = new AppPaths(workspaceRoot: resolvedWorkspace);

// 5. Registar todos os serviços com o appPaths correto
services.AddSingleton<IAppPaths>(appPaths);
services.AddSingleton<ISettingsService>(new SettingsService(appPaths));
// ... resto dos serviços
```

### Fluxo da primeira execução (UI)

```
Arranque da app
    └── IsInitializedAsync() == false
            └── Navegar para FirstRunSetupView
                    ├── Utilizador escolhe workspace root (folder picker)
                    ├── UI chama IWorkspaceService.EnsureWorkspaceRootAsync(path)
                    ├── Se sucesso, guardar nas settings:
                    │       settings.WorkspaceRoot = path
                    │       await settingsService.SaveAsync(settings)
                    └── Reiniciar fluxo normal (navegar para FirstRunAuth)
```

### Nota importante

Após guardar o workspace, o `IAppPaths` precisa de ser atualizado. A solução mais limpa é:
- **Opção A**: Reiniciar a app após a primeira configuração (método mais simples e seguro).
- **Opção B**: `IAppPaths` ser mutável com um método `SetWorkspaceRoot(string)`, mas isto é mais frágil.

**Recomendação**: Opção A. Mostrar mensagem ao utilizador e reiniciar via `Environment.Exit(0)` seguido de `Process.Start`.

---

## 6. IAppPaths revisitado

```csharp
// src/DevTracker.Application/Abstractions/IO/IAppPaths.cs
namespace DevTracker.Application.Abstractions.IO;

public interface IAppPaths
{
    string AppDataRoot { get; }
    string DatabasePath { get; }
    string LogsPath { get; }
    string ConfigPath { get; }

    /// <summary>
    /// Workspace root definido pelo utilizador.
    /// Pode estar vazio antes da primeira configuração.
    /// </summary>
    string WorkspaceRoot { get; }

    bool IsWorkspaceConfigured { get; }
}
```

```csharp
// src/DevTracker.Infrastructure/IO/AppPaths.cs
namespace DevTracker.Infrastructure.IO;

public sealed class AppPaths : IAppPaths
{
    public string AppDataRoot { get; }
    public string DatabasePath { get; }
    public string LogsPath { get; }
    public string ConfigPath { get; }
    public string WorkspaceRoot { get; }
    public bool IsWorkspaceConfigured => !string.IsNullOrWhiteSpace(WorkspaceRoot);

    public AppPaths(string workspaceRoot)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AppDataRoot  = Path.Combine(local, "DevTracker");
        DatabasePath = Path.Combine(AppDataRoot, "data.db");
        LogsPath     = Path.Combine(AppDataRoot, "logs");
        ConfigPath   = Path.Combine(AppDataRoot, "config.json");
        WorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot)
            ? string.Empty
            : Path.GetFullPath(workspaceRoot);
    }

    /// <summary>
    /// Factory helper: lê o config.json de forma síncrona para uso no bootstrap,
    /// antes do DI container estar construído.
    /// </summary>
    public static string ReadWorkspaceRootFromConfig()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var configPath = Path.Combine(local, "DevTracker", "config.json");

        if (!File.Exists(configPath))
            return string.Empty;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("workspaceRoot", out var prop))
                return prop.GetString() ?? string.Empty;
        }
        catch { }

        return string.Empty;
    }
}
```

---

## 7. Validação das Settings

```csharp
// src/DevTracker.Application/Validators/Settings/AppSettingsValidator.cs
using DevTracker.Application.Settings;
using FluentValidation;

namespace DevTracker.Application.Validators.Settings;

public sealed class AppSettingsValidator : AbstractValidator<AppSettings>
{
    public AppSettingsValidator()
    {
        RuleFor(x => x.WorkspaceRoot)
            .NotEmpty().WithMessage("O workspace root é obrigatório.")
            .Must(Directory.Exists).WithMessage("O workspace root não existe no disco.")
            .When(x => !string.IsNullOrWhiteSpace(x.WorkspaceRoot));

        RuleFor(x => x.SessionIdleTimeoutMinutes)
            .InclusiveBetween(1, 480)
            .WithMessage("O timeout de sessão deve estar entre 1 e 480 minutos.");

        RuleFor(x => x.SessionMaxDurationHours)
            .InclusiveBetween(1, 24)
            .WithMessage("A duração máxima da sessão deve estar entre 1 e 24 horas.");

        RuleFor(x => x.Ui.Theme)
            .Must(t => t is "Light" or "Dark" or "System")
            .WithMessage("Tema inválido. Use 'Light', 'Dark' ou 'System'.");
    }
}
```

---

## 8. SettingsViewModel (Refatorado)

O `SettingsViewModel` foi refatorado para usar exclusivamente `ISettingsService`. Não escreve mais em `settings.txt` diretamente.

```csharp
// src/DevTracker.Desktop/ViewModels/Settings/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Application.Settings;
using DevTracker.Application.Validators.Settings;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Settings;

public sealed partial class SettingsViewModel : PageViewModelBase
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private string _workspaceRoot = string.Empty;
    [ObservableProperty] private int _sessionIdleTimeoutMinutes;
    [ObservableProperty] private int _sessionMaxDurationHours;
    [ObservableProperty] private string _theme = "System";
    [ObservableProperty] private string _gitHubUsername = string.Empty;
    [ObservableProperty] private string _gitHubToken = string.Empty;  // Apenas em memória

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.LoadAsync(ct);

        WorkspaceRoot             = settings.WorkspaceRoot;
        SessionIdleTimeoutMinutes = settings.SessionIdleTimeoutMinutes;
        SessionMaxDurationHours   = settings.SessionMaxDurationHours;
        Theme                     = settings.Ui.Theme;
        GitHubUsername            = settings.GitHubUsername;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ClearMessages();

        try
        {
            var updated = new AppSettings
            {
                WorkspaceRoot             = WorkspaceRoot,
                SessionIdleTimeoutMinutes = SessionIdleTimeoutMinutes,
                SessionMaxDurationHours   = SessionMaxDurationHours,
                SchemaVersion             = 1,
                GitHubUsername            = GitHubUsername,
                Ui = new UiPreferences { Theme = Theme }
            };

            var validator = new AppSettingsValidator();
            var validation = await validator.ValidateAsync(updated);

            if (!validation.IsValid)
            {
                SetError(validation.Errors[0].ErrorMessage);
                return;
            }

            var result = await _settingsService.SaveAsync(updated);

            if (!result.Succeeded)
                SetError(result.Error!);
            else
                SetSuccess("Configurações guardadas. Algumas alterações requerem reinício da app.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### Segurança de credenciais

| Propriedade | Persistido | Local |
|-------------|-----------|-------|
| `GitHubUsername` | Sim | `config.json` via `ISettingsService` |
| `GitHubToken` | **Não** | Apenas em memória durante a sessão |

**Regra**: Nunca guardar tokens de acesso em ficheiros de texto plano. O token é recolhido na UI e mantido apenas no campo `_gitHubToken` do ViewModel.

---

## 9. Dependency Injection

```csharp
// src/DevTracker.Infrastructure/DependencyInjection.cs (extensão parcial)
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

public static IServiceCollection AddSettings(this IServiceCollection services)
{
    services.AddSingleton<ISettingsService, SettingsService>();
    return services;
}
```

---

## 10. Regras finais

1. As settings vivem em `config.json`, não na BD SQLite.
2. `ISettingsService.LoadAsync` nunca lança exceção — retorna defaults em caso de erro.
3. O workspace root é validado como existente antes de ser aceite.
4. Após mudar o workspace root, a app deve reiniciar para garantir consistência dos caminhos.
5. `IAppPaths` é construído uma vez no arranque com o workspace root resolvido.
6. `AppPaths.ReadWorkspaceRootFromConfig()` é o único acesso síncrono ao config, para uso no bootstrap antes do DI.
7. **Nunca guardar segredos, passwords ou tokens nas settings.**
8. `GitHubUsername` pode ser persistido; `GitHubToken` é **apenas em memória**.
