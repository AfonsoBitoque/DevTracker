namespace DevTracker.Desktop.ViewModels.Base;

/// <summary>
/// Base para ViewModels de página completa (não-dialogs).
/// OnActivatedAsync é chamado pelo NavigationService quando a página fica visível.
/// Carregar permissões (IPermissionSnapshotService) e dados iniciais aqui.
/// </summary>
public abstract class PageViewModelBase : ViewModelBase
{
    /// <summary>Chamado quando o utilizador navega para esta página.</summary>
    public virtual Task OnActivatedAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Chamado quando o utilizador navega para outra página.</summary>
    public virtual Task OnDeactivatedAsync()
        => Task.CompletedTask;
}
