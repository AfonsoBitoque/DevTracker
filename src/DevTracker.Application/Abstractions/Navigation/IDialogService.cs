namespace DevTracker.Application.Abstractions.Navigation;

/// <summary>
/// Gestão de dialogs modais. Todos os dialogs passam por aqui — nunca instanciar Window nos ViewModels.
/// Implementação em AvaloniaDialogService (Desktop layer).
/// </summary>
public interface IDialogService
{
    /// <summary>Abre um dialog modal. Retorna true se confirmado, false se cancelado.</summary>
    Task<bool> ShowDialogAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : class;

    /// <summary>Abre um dialog modal que devolve um resultado tipado.</summary>
    Task<TResult?> ShowDialogAsync<TViewModel, TResult>(Action<TViewModel>? configure = null) where TViewModel : class;

    Task<bool> ConfirmAsync(string title, string message);
    Task        AlertAsync(string title, string message);
}
