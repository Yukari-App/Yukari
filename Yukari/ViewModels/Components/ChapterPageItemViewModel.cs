using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.ViewModels.Components
{
    public partial class ChapterPageItemViewModel : ObservableObject
    {
        private readonly ChapterPageModel _model;

        [ObservableProperty]
        public partial string? ImageUrl { get; set; }

        [ObservableProperty]
        public partial bool IsLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageMaxWidth), nameof(PageMaxHeight))]
        public partial (double Width, double Height) ScreenSize { get; set; } = (0, 0);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageMaxWidth), nameof(PageMaxHeight))]
        public partial ScalingMode ScalingMode { get; set; } = ScalingMode.FitScreen;

        public double PageMaxWidth =>
            ScalingMode switch
            {
                ScalingMode.FitScreen or ScalingMode.FitWidth => ScreenSize.Width,
                ScalingMode.FitHeight or ScalingMode.OriginalSize => double.PositiveInfinity,
                _ => double.PositiveInfinity,
            };

        public double PageMaxHeight =>
            ScalingMode switch
            {
                ScalingMode.FitScreen or ScalingMode.FitHeight => ScreenSize.Height,
                ScalingMode.FitWidth or ScalingMode.OriginalSize => double.PositiveInfinity,
                _ => double.PositiveInfinity,
            };

        [ObservableProperty]
        public partial bool HasError { get; set; } = false;

        public ChapterPageItemViewModel(ChapterPageModel model)
        {
            _model = model;
            ImageUrl = _model.ImageUrl;
        }

        [RelayCommand]
        public void Retry()
        {
            HasError = false;
            IsLoading = true;

            ImageUrl = null;
            ImageUrl = _model.ImageUrl;
        }

        public void OnLoadSuccess()
        {
            IsLoading = false;
            HasError = false;
        }

        public void OnLoadFailed()
        {
            IsLoading = false;
            HasError = true;
        }
    }
}
