namespace DevTracker.Application.Abstractions.Services;

/// <summary>Serviço para notificações Toast reativas na UI.</summary>
public interface IAppNotificationService
{
    void ShowSuccess(string title, string message);
    void ShowError(string title, string message);
    void ShowInfo(string title, string message);
    void ShowWarning(string title, string message);
}
