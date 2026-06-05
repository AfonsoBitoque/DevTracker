using DevTracker.Application.Abstractions.IO;

namespace DevTracker.Infrastructure.IO;

/// <summary>
/// Implementação imutável de IAppPaths. Construída uma vez no bootstrap.
/// ReadWorkspaceRootFromConfig() lê config.json de forma síncrona antes do DI estar disponível.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public string AppDataRoot           { get; }
    public string DatabasePath          { get; }
    public string LogsPath              { get; }
    public string ConfigPath            { get; }
    public string WorkspaceRoot         { get; }
    public bool   IsWorkspaceConfigured => !string.IsNullOrWhiteSpace(WorkspaceRoot);

    public AppPaths(string workspaceRoot)
    {
        var local    = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AppDataRoot  = Path.Combine(local, "DevTracker");
        DatabasePath = Path.Combine(AppDataRoot, "data.db");
        LogsPath     = Path.Combine(AppDataRoot, "logs");
        ConfigPath   = Path.Combine(AppDataRoot, "config.json");
        WorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot)
            ? string.Empty
            : Path.GetFullPath(workspaceRoot);

        // Garantir que os diretórios de dados existem antes de qualquer operação de BD
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsPath);
    }

    /// <summary>
    /// Leitura síncrona do WorkspaceRoot de config.json para uso no bootstrap (antes do DI).
    /// Retorna string.Empty se o ficheiro não existir, estiver corrompido ou o campo ausente.
    /// </summary>
    public static string ReadWorkspaceRootFromConfig()
    {
        var local      = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var configPath = Path.Combine(local, "DevTracker", "config.json");

        if (!File.Exists(configPath)) return string.Empty;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("workspaceRoot", out var prop)
                ? prop.GetString() ?? string.Empty
                : string.Empty;
        }
        catch { return string.Empty; }
    }
}
