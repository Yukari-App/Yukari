using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yukari.ViewModels.Pages
{
    public partial class DownloadsPageViewModel : ObservableObject
    {
        [ObservableProperty] public partial bool IsPaused { get; set; }

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
