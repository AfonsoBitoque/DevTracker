using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.Services;

/// <summary>Serviço para operações Git locais via CLI.</summary>
public interface IGitService
{
    /// <summary>Clona um repositório git para o caminho especificado (diretório deve estar vazio).</summary>
    Task<Result> CloneAsync(string repositoryUrl, string destinationPath, string? token = null, CancellationToken ct = default);

    /// <summary>Inicializa um repositório git no caminho especificado se não existir.</summary>
    Task<Result> InitAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Cria uma nova branch e muda para ela.</summary>
    Task<Result> CreateBranchAsync(string repositoryPath, string branchName, CancellationToken ct = default);

    /// <summary>Adiciona todos os ficheiros ao staging area.</summary>
    Task<Result> AddAllAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Cria um commit com a mensagem especificada.</summary>
    Task<Result> CommitAsync(string repositoryPath, string message, CancellationToken ct = default);

    /// <summary>Obtém as diferenças entre o working directory e o último commit.</summary>
    Task<Result<string>> GetDiffAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Obtém as diferenças para um ficheiro específico.</summary>
    Task<Result<string>> GetFileDiffAsync(string repositoryPath, string filePath, CancellationToken ct = default);

    /// <summary>Obtém o status dos ficheiros modificados.</summary>
    Task<Result<string>> GetStatusAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Adiciona um remoto se não existir, usando HTTPS com token.</summary>
    Task<Result> AddRemoteAsync(string repositoryPath, string remoteName, string url, string? token = null, CancellationToken ct = default);

    /// <summary>Verifica se há um repositório git inicializado.</summary>
    bool IsGitRepository(string repositoryPath);

    /// <summary>Obtém o nome do branch atual.</summary>
    Task<Result<string>> GetCurrentBranchAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Push para o remoto configurado usando o branch atual.</summary>
    Task<Result> PushAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Pull para o remoto configurado usando o branch atual.</summary>
    Task<Result> PullAsync(string repositoryPath, CancellationToken ct = default);
}
