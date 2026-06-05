using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using DevTracker.Application.Abstractions.Services;

namespace DevTracker.Desktop.Navigation;

/// <summary>
/// Implementação de IAppNotificationService usando o WindowNotificationManager nativo do Avalonia.
/// Usa uma fábrica lazy para obter a janela principal e evitar captive dependencies.
/// </summary>
public sealed class AvaloniaNotificationService(Func<Window> mainWindowFactory) : IAppNotificationService
{
    private WindowNotificationManager? _notificationManager;

    private WindowNotificationManager GetManager()
    {
        if (_notificationManager is null)
        {
            var window = mainWindowFactory();
            _notificationManager = new WindowNotificationManager(window)
            {
                Position = NotificationPosition.BottomRight,
                MaxItems = 3
            };
        }
        return _notificationManager;
    }

    public void ShowSuccess(string title, string message)
        => GetManager().Show(new Notification(title, message, NotificationType.Success));

    public void ShowError(string title, string message)
        => GetManager().Show(new Notification(title, message, NotificationType.Error));

    public void ShowInfo(string title, string message)
        => GetManager().Show(new Notification(title, message, NotificationType.Information));

    public void ShowWarning(string title, string message)
        => GetManager().Show(new Notification(title, message, NotificationType.Warning));
}
