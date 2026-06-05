namespace DevTracker.Application.Abstractions.IO;

/// <summary>
/// Caminhos absolutos do sistema de ficheiros da aplicação.
/// Construído uma vez no bootstrap com o WorkspaceRoot resolvido de config.json.
/// IsWorkspaceConfigured é false antes da primeira configuração (FirstRunSetup).
/// </summary>
public interface IAppPaths
{
    /// <summary>Raiz de dados da app: LocalApplicationData/DevTracker/</summary>
    string AppDataRoot   { get; }
    string DatabasePath  { get; }
    string LogsPath      { get; }
    string ConfigPath    { get; }
    /// <summary>Pasta raiz do workspace escolhida pelo utilizador. Pode ser string.Empty na 1ª execução.</summary>
    string WorkspaceRoot { get; }
    bool   IsWorkspaceConfigured { get; }
}
