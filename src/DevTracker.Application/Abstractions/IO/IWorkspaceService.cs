using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.IO;

/// <summary>
/// Ponto único de entrada para todas as operações de ficheiros do workspace.
/// Nenhum outro serviço, ViewModel ou helper deve usar Directory.* ou File.* para lógica de negócio.
/// Garante que toda a IO fica dentro do WorkspaceRoot (IsInsideWorkspace) e sanitiza nomes.
/// </summary>
public interface IWorkspaceService
{
    Task<Result<string>> EnsureWorkspaceRootAsync(string path, CancellationToken ct = default);

    Task<Result<string>> CreateProjectFolderAsync(Guid projectId, string projectName, CancellationToken ct = default);
    Task<Result<string>> RenameProjectFolderAsync(Guid projectId, string currentAbsolutePath, string newName, CancellationToken ct = default);
    Task<Result>         ArchiveProjectFolderAsync(Guid projectId, string currentAbsolutePath, CancellationToken ct = default);

    Task<Result<string>> CreateRepositoryFolderAsync(Guid projectId, string projectAbsolutePath, string repoName, CancellationToken ct = default);
    Task<Result<string>> RenameRepositoryFolderAsync(Guid repositoryId, string repoAbsolutePath, string newName, CancellationToken ct = default);
    Task<Result>         ArchiveRepositoryFolderAsync(Guid repositoryId, string repoAbsolutePath, CancellationToken ct = default);

    /// <summary>Move/copia pasta externa para dentro do workspace. Suporta cross-volume via Copy+Delete.</summary>
    Task<Result<string>> AttachExistingFolderAsync(Guid projectId, string sourceAbsolutePath, CancellationToken ct = default);

    bool   IsInsideWorkspace(string absolutePath);
    string NormalizeAbsolutePath(string path);

    /// <summary>Lista ficheiros e pastas num path absoluto (read-only). Retorna empty se path inválido.</summary>
    Task<Result<IReadOnlyList<WorkspaceEntry>>> ListEntriesAsync(string absolutePath, CancellationToken ct = default);

    /// <summary>Copia o conteúdo de source (pode ser fora do workspace) para dest (deve estar dentro do workspace).</summary>
    Task<Result> CopyDirectoryContentsAsync(string sourceAbsolutePath, string destinationAbsolutePath, CancellationToken ct = default);
}

public sealed record WorkspaceEntry(string Name, string FullPath, bool IsDirectory);
