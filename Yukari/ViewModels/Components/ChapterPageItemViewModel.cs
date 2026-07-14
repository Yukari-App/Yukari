using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Models;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Components;

public partial class ChapterPageItemViewModel : ObservableObject
{
    private readonly IImageCacheService _imageCacheService;

    private readonly ChapterPageModel _model;
    private readonly ReaderDisplayContext _displayContext;

    [ObservableProperty]
    public partial string? ImageUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebtoonPlaceholderVisible))]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebtoonPlaceholderVisible))]
    public partial bool HasError { get; set; } = false;

    [ObservableProperty]
    public partial double? MeasuredHeight { get; set; }

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

    public double WebtoonMaxWidth => Math.Min(_displayContext.ScreenSize.Width, 900);
    public double PlaceholderWebtoonHeight => WebtoonMaxWidth * 1.32;
    public double WebtoonEffectiveHeight => MeasuredHeight ?? PlaceholderWebtoonHeight;

    public bool IsWebtoonPlaceholderVisible => IsLoading || HasError;

    public ChapterPageItemViewModel(
        IImageCacheService imageCacheService,
        ChapterPageModel model,
        ReaderDisplayContext settings
    )
    {
        _imageCacheService = imageCacheService;

        _model = model;
        _displayContext = settings;
        ImageUrl = _model.ImageUrl;

        if (_imageCacheService.TryGetCached(ImageUrl, out _))
            IsLoading = false;

        _displayContext.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(WebtoonMaxWidth));
            OnPropertyChanged(nameof(PlaceholderWebtoonHeight));
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

    public void SetMeasuredHeight(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0)
            return;
        MeasuredHeight = WebtoonMaxWidth * ((double)pixelHeight / pixelWidth);
    }

    public async Task EnsureMeasuredAsync()
    {
        if (MeasuredHeight != null)
            return;

        var dimensions = await _imageCacheService.TryGetDimensionsAsync(_model.ImageUrl);
        if (dimensions != null)
            SetMeasuredHeight(dimensions.Value.Width, dimensions.Value.Height);
    }

    public void RefreshCacheState()
    {
        if (_imageCacheService.TryGetCached(ImageUrl, out _))
            IsLoading = false;
        else if (!IsLoading && !HasError)
            // This entry was evicted from the cache (likely FIFO) since the page was last displayed.
            // The ImageUrl binding will trigger a new load when this ChapterPageItemViewModel
            // re-enters the visible Webtoon viewport. We reflect this here so the placeholder/spinner
            // shows again instead of incorrectly displaying a stale "ready" image.
            IsLoading = true;
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
