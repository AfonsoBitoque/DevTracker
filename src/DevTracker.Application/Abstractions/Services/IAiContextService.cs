using DevTracker.Application.Common;

namespace DevTracker.Application.Abstractions.Services;

/// <summary>
/// Compila os dados de um Work Item num bloco Markdown otimizado para LLMs.
/// </summary>
public interface IAiContextService
{
    Task<Result<string>> PackTaskContextAsync(Guid workItemId, CancellationToken ct = default);
}
