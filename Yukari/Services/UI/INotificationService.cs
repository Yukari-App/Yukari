using Yukari.Enums;

namespace Yukari.Services.UI
{
    public interface INotificationService
    {
        void Show(string message, string title, NotificationSeverity severity);

        void ShowSuccess(string message, string title = "Success");
        void ShowInfo(string message, string title = "Info");
        void ShowError(string message, string title = "Error");
        void ShowWarning(string message, string title = "Warning");
    }
}
