using System.IO;
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Infrastructure.IO;
using DevTracker.Infrastructure.Persistence;
using DevTracker.Infrastructure.Security;
using DevTracker.Infrastructure.Services;
using DevTracker.Infrastructure.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Infrastructure;

/// <summary>
/// Registo de todos os serviços da Infrastructure na container de DI.
/// Chamar de Program.cs após construir AppPaths com o workspace resolvido.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string                  databasePath,
        IAppPaths               appPaths)
    {
        // EF Core com SQLite
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlite($"Data Source={databasePath}"));

        // Singletons — estado global da aplicação
        services.AddSingleton<ISessionService,             InMemorySessionService>();
        services.AddSingleton<ICurrentUserService,         CurrentUserService>();
        services.AddSingleton<ICriticalActionTokenService, CriticalActionTokenService>();

        // Scoped — ciclo de vida por operação/request
        services.AddScoped<IPasswordHasher,            Argon2PasswordHasher>();
        services.AddScoped<IPermissionService,         PermissionService>();
        services.AddScoped<IPermissionSnapshotService, PermissionSnapshotService>();
        services.AddScoped<IAuthService,               AuthService>();
        services.AddScoped<IReAuthenticationService>(sp => (AuthService)sp.GetRequiredService<IAuthService>());

        // Serviços de domínio
        services.AddScoped<IAuditService,       AuditService>();
        services.AddScoped<IProjectService,     ProjectService>();
        services.AddScoped<IWorkItemService,    WorkItemService>();
        services.AddScoped<IRepositoryService,  RepositoryService>();
        services.AddScoped<IUserService,        UserService>();
        services.AddScoped<IDashboardService,   DashboardService>();
        services.AddScoped<IAiContextService,   AiContextService>();

        // IO & Settings
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // Git & GitHub
        services.AddTransient<IGitService, GitService>();
        services.AddTransient<IGitHubService, GitHubService>();

        // Data Protection & Secrets
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(appPaths.AppDataRoot, "keys")))
            .SetApplicationName("DevTracker");

        services.AddSingleton<ISecretService, SecretService>();

        return services;
    }

    /// <summary>
    /// Garante que a BD SQLite existe com o schema correto.
    /// Usa EnsureCreated (não Migrate) pois não há ficheiros de migration EF gerados.
    /// </summary>
    public static void EnsureDatabase(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }
}
