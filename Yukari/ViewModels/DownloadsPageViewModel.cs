using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yukari.ViewModels
{
    public partial class DownloadsPageViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isPaused;

        public string PauseIcon => IsPaused ? "\uE769" : "\uE768";

        public DownloadsPageViewModel()
        {
            
        }

        [RelayCommand]
        public void TogglePause()
        {
            IsPaused = !IsPaused;

            OnPropertyChanged(nameof(PauseIcon));
        }
    }
}
