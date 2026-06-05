namespace DevTracker.Application.Abstractions.Navigation;

/// <summary>
/// Navegação entre páginas via substituição do CurrentPage no ShellViewModel.
/// Toda a navegação passa por aqui — nunca criar Views diretamente em ViewModels.
/// </summary>
public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo<TViewModel>(Action<TViewModel> configure) where TViewModel : class;
    void NavigateBack();
    void NavigateForward();
    bool CanNavigateBack { get; }
    bool CanNavigateForward { get; }

    /// <summary>Disparado quando CanNavigateBack ou CanNavigateForward mudam.</summary>
    event Action? StateChanged;
}
