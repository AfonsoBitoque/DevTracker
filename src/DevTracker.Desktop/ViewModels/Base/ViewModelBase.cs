using CommunityToolkit.Mvvm.ComponentModel;

namespace DevTracker.Desktop.ViewModels.Base;

/// <summary>
/// Base comum a todos os ViewModels. Fornece IsBusy, mensagens de erro/sucesso
/// e helpers para o padrão de comando async.
/// Views são passivas — zero lógica de negócio no code-behind.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool    _isBusy;
    [ObservableProperty] private string? _busyMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    protected void SetBusy(bool isBusy, string? message = null)
    {
        IsBusy = isBusy;
        BusyMessage = isBusy ? message : null;
        if (!isBusy) ClearMessages();
    }

    protected void ClearMessages()
    {
        ErrorMessage   = null;
        BusyMessage    = null;
        SuccessMessage = null;
    }

    protected void SetError(string message)
    {
        ErrorMessage   = message;
        SuccessMessage = null;
    }

    protected void SetSuccess(string message)
    {
        SuccessMessage = message;
        ErrorMessage   = null;
        BusyMessage    = null;
    }
}
