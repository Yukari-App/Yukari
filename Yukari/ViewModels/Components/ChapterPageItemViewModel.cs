using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.ViewModels.Components;

public partial class ChapterPageItemViewModel : ObservableObject
{
    private readonly ChapterPageModel _model;
    private readonly ReaderDisplayContext _displayContext;

    [ObservableProperty]
    public partial string? ImageUrl { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    public double PageMaxWidth =>
        _displayContext.ScalingMode switch
        {
            ScalingMode.FitScreen or ScalingMode.FitWidth => _displayContext.ScreenSize.Width,
            _ => double.PositiveInfinity,
        };

    public double PageMaxHeight =>
        _displayContext.ScalingMode switch
        {
            ScalingMode.FitScreen or ScalingMode.FitHeight => _displayContext.ScreenSize.Height,
            _ => double.PositiveInfinity,
        };

    [ObservableProperty]
    public partial bool HasError { get; set; } = false;

    public ChapterPageItemViewModel(ChapterPageModel model, ReaderDisplayContext settings)
    {
        _model = model;
        _displayContext = settings;
        ImageUrl = _model.ImageUrl;

        _displayContext.PropertyChanged += (_, e) =>
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
