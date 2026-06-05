using System.Diagnostics;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;

namespace DevTracker.Infrastructure.Services;

/// <summary>Serviço para operações Git locais via CLI.</summary>
public sealed class GitService : IGitService
{
    /// <summary>Clona um repositório git para o caminho especificado (diretório deve estar vazio).</summary>
    public async Task<Result> CloneAsync(string repositoryUrl, string destinationPath, string? token = null, CancellationToken ct = default)
    {
        // Converter SSH URL para HTTPS se necessário
        var httpsUrl = ConvertToHttpsUrl(repositoryUrl);

        // Add token à URL se fornecido
        if (!string.IsNullOrWhiteSpace(token))
        {
            httpsUrl = httpsUrl.Replace("https://", $"https://{token}@");
        }

        // Clonar repositório para o diretório especificado (cria subpasta com nome do repo)
        var result = await RunGitCommandAsync(destinationPath, $"clone {httpsUrl} .", ct);
        if (!result.Succeeded) return Result.Fail(result.Error!);

        // Configurar user.name
        var configResult = await RunGitCommandAsync(destinationPath, "config user.name DevTracker", ct);
        if (!configResult.Succeeded) return Result.Fail(configResult.Error!);

        return Result.Success();
    }

    private static string ConvertToHttpsUrl(string url)
    {
        // Converter SSH URL para HTTPS
        if (url.StartsWith("git@github.com:"))
        {
            return url.Replace("git@github.com:", "https://github.com/");
        }
        return url;
    }

    /// <summary>Inicializa um repositório git no caminho especificado se não existir.</summary>
    public async Task<Result> InitAsync(string repositoryPath, CancellationToken ct = default)
    {
        // Inicializar apenas se ainda não existir um repositório
        if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
        {
            var result = await RunGitCommandAsync(repositoryPath, "init", ct);
            if (!result.Succeeded) return Result.Fail(result.Error!);
        }

        // Garantir identidade local (idempotente) — sem isto, o commit falha
        // em máquinas sem configuração git global.
        var nameResult = await RunGitCommandAsync(repositoryPath, "config user.name DevTracker", ct);
        if (!nameResult.Succeeded) return Result.Fail(nameResult.Error!);

        var emailResult = await RunGitCommandAsync(repositoryPath, "config user.email devtracker@local", ct);
        if (!emailResult.Succeeded) return Result.Fail(emailResult.Error!);

        return Result.Success();
    }

    /// <summary>Cria uma nova branch e muda para ela.</summary>
    public async Task<Result> CreateBranchAsync(string repositoryPath, string branchName, CancellationToken ct = default)
    {
        // Sanitizar o nome da branch (remover caracteres inválidos)
        var sanitizedBranchName = SanitizeBranchName(branchName);
        
        // Create e mudar para a nova branch
        var result = await RunGitCommandAsync(repositoryPath, $"checkout -b {sanitizedBranchName}", ct);
        if (result.Succeeded) return Result.Success();

        // Se a branch já existe, apenas muda para ela em vez de falhar.
        if ((result.Error ?? string.Empty).Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            var checkout = await RunGitCommandAsync(repositoryPath, $"checkout {sanitizedBranchName}", ct);
            return checkout.Succeeded ? Result.Success() : Result.Fail(checkout.Error!);
        }

        return Result.Fail(result.Error!);
    }

    private static string SanitizeBranchName(string branchName)
    {
        // Remover caracteres inválidos para nomes de branch git
        var invalidChars = new[] { ' ', '~', '^', ':', '?', '*', '[', ']', '\\', '/', '{', '}', '(', ')', '@', '|', '<', '>', ';', '&', '$', '=', '!', '#', '\'', '"', '`', '\0' };
        var sanitized = branchName;
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '-');
        }
        // Remover pontos consecutivos e espaços
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\.+", ".");
        sanitized = sanitized.Trim('.', '-');
        // Limitar tamanho
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 100);
        }
        return sanitized;
    }

    /// <summary>Adiciona todos os ficheiros ao staging area.</summary>
    public async Task<Result> AddAllAsync(string repositoryPath, CancellationToken ct = default)
    {
        var result = await RunGitCommandAsync(repositoryPath, "add .", ct);
        return result.Succeeded ? Result.Success() : Result.Fail(result.Error!);
    }

    /// <summary>Cria um commit com a mensagem especificada.</summary>
    public async Task<Result> CommitAsync(string repositoryPath, string message, CancellationToken ct = default)
    {
        var result = await RunGitCommandAsync(repositoryPath, $"commit -m \"{message}\"", ct);
        if (result.Succeeded) return Result.Success();

        // Sem alterações no working tree — criar um commit vazio para que o branch
        // ainda assim difira do base (evita "no commits between" no GitHub).
        var combined = $"{result.Error} {result.Value}";
        if (combined.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("nothing added to commit", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("working tree clean", StringComparison.OrdinalIgnoreCase))
        {
            var emptyResult = await RunGitCommandAsync(repositoryPath, $"commit --allow-empty -m \"{message}\"", ct);
            return emptyResult.Succeeded ? Result.Success() : Result.Fail(emptyResult.Error!);
        }

        return Result.Fail(result.Error!);
    }

    /// <summary>Obtém as diferenças entre o working directory e o último commit.</summary>
    public async Task<Result<string>> GetDiffAsync(string repositoryPath, CancellationToken ct = default)
        => await RunGitCommandAsync(repositoryPath, "diff", ct);

    /// <summary>Obtém as diferenças para um ficheiro específico.</summary>
    public async Task<Result<string>> GetFileDiffAsync(string repositoryPath, string filePath, CancellationToken ct = default)
        => await RunGitCommandAsync(repositoryPath, $"diff \"{filePath}\"", ct);

    /// <summary>Obtém o status dos ficheiros modificados.</summary>
    public async Task<Result<string>> GetStatusAsync(string repositoryPath, CancellationToken ct = default)
        => await RunGitCommandAsync(repositoryPath, "status --short", ct);

    /// <summary>Adiciona um remoto se não existir, usando HTTPS com token.</summary>
    public async Task<Result> AddRemoteAsync(string repositoryPath, string remoteName, string url, string? token = null, CancellationToken ct = default)
    {
        var remotes = await RunGitCommandAsync(repositoryPath, "remote", ct);
        if (!remotes.Succeeded) return Result.Fail(remotes.Error!);

        // Converter SSH URL para HTTPS se necessário
        var httpsUrl = ConvertToHttpsUrl(url);

        // Add token à URL se fornecido
        if (!string.IsNullOrWhiteSpace(token))
        {
            httpsUrl = httpsUrl.Replace("https://", $"https://{token}@");
        }

        if (remotes.Value!.Contains(remoteName))
        {
            // Refresh URL do remoto existente
            var result = await RunGitCommandAsync(repositoryPath, $"remote set-url {remoteName} {httpsUrl}", ct);
            return result.Succeeded ? Result.Success() : Result.Fail(result.Error!);
        }

        var addResult = await RunGitCommandAsync(repositoryPath, $"remote add {remoteName} {httpsUrl}", ct);
        return addResult.Succeeded ? Result.Success() : Result.Fail(addResult.Error!);
    }

    /// <summary>Verifica se há um repositório git inicializado.</summary>
    public bool IsGitRepository(string repositoryPath)
        => Directory.Exists(Path.Combine(repositoryPath, ".git"));

    /// <summary>Obtém o nome do branch atual.</summary>
    public async Task<Result<string>> GetCurrentBranchAsync(string repositoryPath, CancellationToken ct = default)
        => await RunGitCommandAsync(repositoryPath, "rev-parse --abbrev-ref HEAD", ct);

    /// <summary>Push para o remoto configurado usando o branch atual.</summary>
    public async Task<Result> PushAsync(string repositoryPath, CancellationToken ct = default)
    {
        var branchResult = await GetCurrentBranchAsync(repositoryPath, ct);
        if (!branchResult.Succeeded) return Result.Fail(branchResult.Error!);

        var branch = branchResult.Value?.Trim() ?? "main";
        
        // Tentar push normal primeiro
        var result = await RunGitCommandAsync(repositoryPath, $"push origin {branch}", ct);

        // Se falhar por falta de upstream, retry com -u (primeiro push do branch)
        if (!result.Succeeded && (result.Error ?? string.Empty).Contains("upstream", StringComparison.OrdinalIgnoreCase))
        {
            result = await RunGitCommandAsync(repositoryPath, $"push -u origin {branch}", ct);
        }

        // Se ainda falhar, tentar force push
        if (!result.Succeeded)
        {
            result = await RunGitCommandAsync(repositoryPath, $"push origin {branch} --force", ct);
        }

        return result.Succeeded ? Result.Success() : Result.Fail(result.Error!);
    }

    /// <summary>Pull para o remoto configurado usando o branch atual.</summary>
    public async Task<Result> PullAsync(string repositoryPath, CancellationToken ct = default)
    {
        var branchResult = await GetCurrentBranchAsync(repositoryPath, ct);
        if (!branchResult.Succeeded) return Result.Fail(branchResult.Error!);

        var branch = branchResult.Value?.Trim() ?? "main";
        
        // Configurar pull para usar merge (rebase false)
        await RunGitCommandAsync(repositoryPath, "config pull.rebase false", ct);
        
        // Add --allow-unrelated-histories para permitir fusão de histórias diferentes
        var result = await RunGitCommandAsync(repositoryPath, $"pull origin {branch} --allow-unrelated-histories", ct);
        
        // Se o pull falhar, tentar fazer merge manual
        if (!result.Succeeded)
        {
            var mergeResult = await RunGitCommandAsync(repositoryPath, $"merge origin/{branch} --allow-unrelated-histories", ct);
            if (!mergeResult.Succeeded) return Result.Fail(mergeResult.Error!);
        }
        
        return Result.Success();
    }

    private async Task<Result<string>> RunGitCommandAsync(string repositoryPath, string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Forçar locale C para garantir mensagens em inglês (matching de erro consistente)
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LANG"] = "C";

        // Add configuração SSH para evitar bloqueio de host key
        startInfo.Environment["GIT_SSH_COMMAND"] = "ssh -o StrictHostKeyChecking=no";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                // O git escreve algumas mensagens (ex: "nothing to commit") em stdout,
                // não em stderr. Combinar ambos para não perder o motivo da falha.
                var details = string.Join(" ", new[] { error, output }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()));
                if (string.IsNullOrWhiteSpace(details))
                    details = $"git {arguments} terminou com código {process.ExitCode}.";
                return Result<string>.Fail($"Git error: {details}");
            }

            return Result<string>.Success(output);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to run git: {ex.Message}");
        }
    }
}
