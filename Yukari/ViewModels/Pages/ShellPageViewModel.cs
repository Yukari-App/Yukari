using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Yukari.Messages;
using Yukari.Models;

namespace Yukari.ViewModels.Pages
{
    public partial class ShellPageViewModel : ObservableRecipient, IRecipient<ShowNotificationMessage>
    {
        public ObservableCollection<NotificationModel> Notifications { get; } = new();

        public ShellPageViewModel(IMessenger messenger) : base(messenger)
        {
            IsActive = true;
        }

        public async void Receive(ShowNotificationMessage message)
        {
            var notification = message.Notification;

            Notifications.Add(notification);

            await Task.Delay(5000);
            Notifications.Remove(notification);
        }
    }
}
