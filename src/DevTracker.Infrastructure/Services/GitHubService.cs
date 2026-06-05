using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;

namespace DevTracker.Infrastructure.Services;

/// <summary>Serviço para integração com GitHub API.</summary>
public sealed class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;

    public GitHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DevTracker");
    }

    /// <summary>Configura o token de autenticação do GitHub.</summary>
    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
    }

    /// <summary>Verifica se o repositório existe e é acessível.</summary>
    public async Task<Result<bool>> RepositoryExistsAsync(string owner, string repo, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}", ct);
            return Result<bool>.Success(response.IsSuccessStatusCode);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail($"Failed to check repository: {ex.Message}");
        }
    }

    /// <summary>Cria um novo repositório no GitHub. Retorna o clone_url.</summary>
    public async Task<Result<string>> CreateRepositoryAsync(string name, string description, bool isPrivate, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                name = name,
                description = description,
                @private = isPrivate,
                auto_init = true
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.github.com/user/repos", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<string>.Fail($"GitHub API error: {error}");
            }

            var resultJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(resultJson);
            var cloneUrl = doc.RootElement.GetProperty("clone_url").GetString();
            return Result<string>.Success(cloneUrl!);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to create repository: {ex.Message}");
        }
    }

    /// <summary>Obtém o nome do branch default do repositório.</summary>
    public async Task<Result<string>> GetDefaultBranchAsync(string repoUrl, string token, CancellationToken ct = default)
    {
        try
        {
            var (owner, repo) = ParseRepoUrl(repoUrl);
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
                return Result<string>.Fail("URL do repositório inválido.");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "DevTracker");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            var response = await client.GetAsync($"https://api.github.com/repos/{owner}/{repo}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                return Result<string>.Fail($"GitHub API error ({(int)response.StatusCode}): {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(responseBody);
            var defaultBranch = node?["default_branch"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(defaultBranch))
                return Result<string>.Fail("Não foi possível obter o branch default do repositório.");

            return Result<string>.Success(defaultBranch);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Erro ao obter branch default: {ex.Message}");
        }
    }

    /// <summary>Cria um Pull Request no GitHub. Retorna o URL do PR criado.</summary>
    public async Task<Result<string>> CreatePullRequestAsync(
        string repoUrl,
        string title,
        string body,
        string headBranch,
        string baseBranch,
        string token,
        CancellationToken ct = default)
    {
        try
        {
            var (owner, repo) = ParseRepoUrl(repoUrl);
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
                return Result<string>.Fail("URL do repositório inválido. Formato esperado: https://github.com/owner/repo");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "DevTracker");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            var payload = new
            {
                title = title,
                body = body,
                head = headBranch,
                @base = baseBranch
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"https://api.github.com/repos/{owner}/{repo}/pulls", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                return Result<string>.Fail($"GitHub API error ({(int)response.StatusCode}): {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(responseBody);
            var htmlUrl = node?["html_url"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(htmlUrl))
                return Result<string>.Fail("PR criado mas não foi possível obter o URL.");

            return Result<string>.Success(htmlUrl);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Erro ao criar PR: {ex.Message}");
        }
    }

    private static (string? Owner, string? Repo) ParseRepoUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return (null, null);

        try
        {
            var uri = new Uri(url.Trim().TrimEnd('/'));
            if (uri.Host != "github.com") return (null, null);

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
                return (segments[0], segments[1]);
        }
        catch { /* invalid URL */ }

        return (null, null);
    }

    /// <summary>Obtém a URL clone do repositório.</summary>
    public static string GetCloneUrl(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl)) return string.Empty;

        // Converter https://github.com/user/repo para git@github.com:user/repo.git
        if (repositoryUrl.StartsWith("https://github.com/"))
        {
            var parts = repositoryUrl.Replace("https://github.com/", "").Split('/');
            if (parts.Length >= 2)
                return $"git@github.com:{parts[0]}/{parts[1]}.git";
        }

        return repositoryUrl;
    }
}
