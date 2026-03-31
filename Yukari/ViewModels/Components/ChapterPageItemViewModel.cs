using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.ViewModels.Components
{
    public partial class ChapterPageItemViewModel : ObservableObject
    {
        private readonly ChapterPageModel _model;
        private readonly ReaderDisplaySettings _settings;

        [ObservableProperty]
        public partial string? ImageUrl { get; set; }

        [ObservableProperty]
        public partial bool IsLoading { get; set; } = true;

        public double PageMaxWidth =>
            _settings.ScalingMode switch
            {
                ScalingMode.FitScreen or ScalingMode.FitWidth => _settings.ScreenSize.Width,
                _ => double.PositiveInfinity,
            };

        public double PageMaxHeight =>
            _settings.ScalingMode switch
            {
                ScalingMode.FitScreen or ScalingMode.FitHeight => _settings.ScreenSize.Height,
                _ => double.PositiveInfinity,
            };

        [ObservableProperty]
        public partial bool HasError { get; set; } = false;

        public ChapterPageItemViewModel(ChapterPageModel model, ReaderDisplaySettings settings)
        {
            _model = model;
            _settings = settings;
            ImageUrl = _model.ImageUrl;

            _settings.PropertyChanged += (_, e) =>
            {
                OnPropertyChanged(nameof(PageMaxWidth));
                OnPropertyChanged(nameof(PageMaxHeight));
            };
        }

        public void OnLoadSuccess() => IsLoading = false;

        public void OnLoadFailed()
        {
            IsLoading = false;
            HasError = true;
        }

        [RelayCommand]
        private void Retry()
        {
            HasError = false;
            IsLoading = true;

            ImageUrl = null;
            ImageUrl = _model.ImageUrl;
        }
    }
}
