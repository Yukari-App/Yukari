using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Models;

namespace Yukari.ViewModels.Components
{
    public partial class ChapterPageItemViewModel : ObservableObject
    {
        public ChapterPageModel Model { get; }

        [ObservableProperty] public partial string ImageUrl { get; set; }
        [ObservableProperty] public partial bool IsLoading { get; set; } = true;
        [ObservableProperty] public partial bool HasError { get; set; } = false;

        public ChapterPageItemViewModel(ChapterPageModel model)
        {
            Model = model;
            ImageUrl = model.ImageUrl;
        }

        [RelayCommand]
        public void Retry()
        {
            HasError = false;
            IsLoading = true;

            var temp = ImageUrl;
            ImageUrl = null!;
            ImageUrl = temp;
        }

        public void OnLoadSuccess() { IsLoading = false; HasError = false; }
        public void OnLoadFailed() { IsLoading = false; HasError = true; }
    }
}
