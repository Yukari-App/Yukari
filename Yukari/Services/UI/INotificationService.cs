using Yukari.Enums;

namespace Yukari.Services.UI;

public interface INotificationService
{
    void Show(string message, string title, NotificationSeverity severity);

    void ShowSuccess(string message, string? title = null);
    void ShowInfo(string message, string? title = null);
    void ShowError(string message, string? title = null);
    void ShowWarning(string message, string? title = null);

    void OnShellReady();
}
