using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.Services;

/// <summary>Serviço para integração com GitHub API.</summary>
public interface IGitHubService
{
    /// <summary>Configura o token de autenticação do GitHub.</summary>
    void SetAuthToken(string token);

    /// <summary>Verifica se o repositório existe e é acessível.</summary>
    Task<Result<bool>> RepositoryExistsAsync(string owner, string repo, CancellationToken ct = default);

    /// <summary>Cria um novo repositório no GitHub.</summary>
    Task<Result<string>> CreateRepositoryAsync(string name, string description, bool isPrivate, CancellationToken ct = default);

    /// <summary>Obtém o nome do branch default do repositório (ex: main, master).</summary>
    Task<Result<string>> GetDefaultBranchAsync(string repoUrl, string token, CancellationToken ct = default);

    /// <summary>Cria um Pull Request no GitHub. Retorna o URL do PR criado.</summary>
    Task<Result<string>> CreatePullRequestAsync(
        string repoUrl,
        string title,
        string body,
        string headBranch,
        string baseBranch,
        string token,
        CancellationToken ct = default);
}
