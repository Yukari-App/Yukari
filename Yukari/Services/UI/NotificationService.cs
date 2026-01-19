using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Models;

namespace Yukari.Services.UI
{
    internal class NotificationService : INotificationService
    {
        private readonly IMessenger _messenger;

        public NotificationService(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public void Show(string message, string title, NotificationSeverity severity)
        {
            var notification = new NotificationModel
            {
                Title = title,
                Message = message,
                Severity = severity,
                IsActive = true
            };

            _messenger.Send(new ShowNotificationMessage(notification));
        }

        public void ShowError(string message, string title = "Error")
            => Show(message, title, NotificationSeverity.Error);

        public void ShowInfo(string message, string title = "Info")
            => Show(message, title, NotificationSeverity.Info);

        public void ShowSuccess(string message, string title = "Success")
            => Show(message, title, NotificationSeverity.Success);

        public void ShowWarning(string message, string title = "Warning")
            => Show(message, title, NotificationSeverity.Warning);
    }
}