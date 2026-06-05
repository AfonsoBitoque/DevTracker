namespace DevTracker.Application.Abstractions.Security;

/// <summary>
/// Carrega todas as permissões do utilizador atual de uma vez, em cache por instância (Scoped).
/// Obrigatório nos ViewModels para evitar N queries por ecrã.
/// NÃO substitui IPermissionService nos serviços — é apenas para adaptar a UI.
/// </summary>
public interface IPermissionSnapshotService
{
    /// <summary>
    /// Devolve o conjunto de permissões do utilizador autenticado.
    /// Resultado é cached em memória até InvalidateCache() ou fim do Scope.
    /// </summary>
    Task<HashSet<string>> GetCurrentUserPermissionsAsync(CancellationToken ct = default);

    /// <summary>Invalida o cache, forçando nova leitura na próxima chamada. Usar após mudança de role.</summary>
    void InvalidateCache();
}
