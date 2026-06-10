using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Pages;

public partial class ShellPageViewModel : ObservableRecipient, IRecipient<ShowNotificationMessage>
{
    public ObservableCollection<NotificationModel> Notifications { get; } = new();

    public ShellPageViewModel(IMessenger messenger, INotificationService notificationService)
        : base(messenger)
    {
        IsActive = true;
        notificationService.OnShellReady();
    }

    public async void Receive(ShowNotificationMessage message)
    {
        var notification = message.Notification;

        Notifications.Add(notification);

        await Task.Delay(10000);
        Notifications.Remove(notification);
    }
}
