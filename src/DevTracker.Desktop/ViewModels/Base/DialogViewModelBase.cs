namespace DevTracker.Desktop.ViewModels.Base;

/// <summary>
/// Base para dialogs modais simples (confirmação/cancelamento).
/// AvaloniaDialogService faz wire no evento CloseRequested para fechar a janela.
/// </summary>
public abstract class DialogViewModelBase : ViewModelBase
{
    /// <summary>Invocado pelo VM para sinalizar ao dialog service que deve fechar.</summary>
    public event Action<bool>? CloseRequested;

    protected void Confirm() => CloseRequested?.Invoke(true);
    protected void Cancel()  => CloseRequested?.Invoke(false);
}

/// <summary>
/// Base para dialogs que devolvem um resultado tipado.
/// Usar quando o dialog precisa de devolver dados ao ViewModel chamador.
/// </summary>
public abstract class DialogViewModelBase<TResult> : ViewModelBase
{
    public event Action<TResult?>? CloseWithResult;

    protected void ConfirmWithResult(TResult result) => CloseWithResult?.Invoke(result);
    protected void Cancel()                          => CloseWithResult?.Invoke(default);
}
