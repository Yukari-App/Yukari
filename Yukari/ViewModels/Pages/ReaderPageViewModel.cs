using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Messages;

namespace Yukari.ViewModels.Pages
{
    public partial class ReaderPageViewModel : ObservableObject
    {
        private readonly IMessenger _messenger;

        public ReaderPageViewModel(IMessenger messenger)
        {
            _messenger = messenger;
        }

        [RelayCommand]
        public void GoBack() => _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));
    }
}
