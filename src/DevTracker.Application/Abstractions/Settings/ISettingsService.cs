using DevTracker.Application.Common;
using DevTracker.Application.Settings;

namespace DevTracker.Application.Abstractions.Settings;

/// <summary>
/// Configuração persistida em config.json (não na BD SQLite).
/// LoadAsync nunca lança exceção — retorna defaults se o ficheiro não existir ou estiver corrompido.
/// </summary>
public interface ISettingsService
{
    /// <summary>Carrega as settings do ficheiro. Retorna defaults em caso de ausência ou erro.</summary>
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    Task<Result> SaveAsync(AppSettings settings, CancellationToken ct = default);

    /// <summary>True se WorkspaceRoot está definido e a pasta existe no disco.</summary>
    Task<bool> IsInitializedAsync(CancellationToken ct = default);

    /// <summary>Acesso síncrono às settings carregadas em memória (após LoadAsync).</summary>
    AppSettings Current { get; }
}
