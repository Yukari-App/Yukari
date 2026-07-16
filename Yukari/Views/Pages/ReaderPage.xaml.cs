using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Yukari.Enums;
using Yukari.Models.DTO;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;
using Yukari.Views.Controls;

namespace Yukari.Views.Pages;

public sealed partial class ReaderPage : Page
{
    public ReaderPageViewModel ViewModel { get; set; }

    private CancellationTokenSource? _sliderDebounceCts;

    private Point _lastMousePosition;
    private double _startHorizontalOffset;
    private double _startVerticalOffset;
    private bool _isDragging = false;
    private CancellationTokenSource? _positioningOverlayCts;

    // Webtoon Fields
    private const int DefaultWindowRadius = 4;
    private int _windowStartIndex;
    private int _windowEndIndex;
    private double[]? _cachedOffsets;
    private bool _isScrollingToTarget;

    private ObservableCollection<ChapterPageItemViewModel> VisibleWebtoonPages { get; } = new();

    public ReaderPage()
    {
        InitializeComponent();

        ViewModel = App.GetService<ReaderPageViewModel>();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ReaderNavigationArgs args)
        {
            await ViewModel.InitializeAsync(
                args.ComicKey,
                args.ComicTitle,
                args.ChapterKey,
                args.SelectedLang,
                args.NavigationFromContinueButton
            );
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

        _sliderDebounceCts?.Cancel();
        _sliderDebounceCts?.Dispose();
        _sliderDebounceCts = null;
    }

    #region ViewModel Events

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
            is nameof(ReaderPageViewModel.ReadingMode)
                or nameof(ReaderPageViewModel.ChapterState)
        )
        {
            if (!ViewModel.IsLoaded)
            {
                ClearVisibleWebtoonPages();
                return;
            }

            if (ViewModel.ReadingMode == ReadingMode.Webtoon)
                ScrollToPageIndex(ViewModel.WebtoonPageIndex, isInitialResume: true);
            else
                _ = ForceFlipViewUpdateAsync();
        }
        else if (e.PropertyName == nameof(ReaderPageViewModel.CurrentPageIndex))
            if (ViewModel.ReadingMode == ReadingMode.Webtoon && ViewModel.IsLoaded)
                ScrollToPageIndex(ViewModel.CurrentPageIndex);
    }

    #endregion

    #region TitleBar & ContentSection

    private void ContentSection_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsFullscreen)
            AppTitleBar.Visibility = Visibility.Collapsed;
    }

    private void ContentSection_PointerExited(object sender, PointerRoutedEventArgs e) =>
        AppTitleBar.Visibility = Visibility.Visible;

    private void ContentSection_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Grid grid)
            UpdateScreenSize(grid.ActualWidth, grid.ActualHeight);
    }

    private void ContentSection_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateScreenSize(e.NewSize.Width, e.NewSize.Height);

    private void UpdateScreenSize(double width, double height) =>
        ViewModel.SetScreenSizeCommand.Execute((width, height));

    private void PageIndicator_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.ChapterPages == null || ViewModel.ChapterPages.Count <= 1)
            return;

        var isHorizontal = (ShadowedText)sender == HorizontalPageIndicator;
        var flyout = new Flyout
        {
            Placement = isHorizontal ? FlyoutPlacementMode.Top : FlyoutPlacementMode.Left,
            ShouldConstrainToRootBounds = true,
        };

        var presenterStyle = new Style(typeof(FlyoutPresenter));
        presenterStyle.Setters.Add(new Setter(MinHeightProperty, 0));
        presenterStyle.Setters.Add(new Setter(MinWidthProperty, 0));
        presenterStyle.Setters.Add(new Setter(CornerRadiusProperty, new CornerRadius(8)));
        presenterStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
        flyout.FlyoutPresenterStyle = presenterStyle;

        var slider = new Slider
        {
            Margin = isHorizontal ? new Thickness(12, 4, 12, 4) : new Thickness(4, 12, 4, 12),
            Minimum = 1,
            Orientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical,
            Maximum = ViewModel.ChapterPages.Count,
            Value = ViewModel.CurrentPageForDisplay,
            IsDirectionReversed = ViewModel.ReadingMode != ReadingMode.LeftToRight,
            Height = isHorizontal ? 32 : 380,
            Width = isHorizontal ? 380 : 32,
        };

        slider.ValueChanged += async (s, args) =>
        {
            _sliderDebounceCts?.Cancel();
            _sliderDebounceCts?.Dispose();
            _sliderDebounceCts = new();

            try
            {
                await Task.Delay(350, _sliderDebounceCts.Token);
                ViewModel.JumpToPageCommand.Execute((int)slider.Value - 1);
            }
            catch (TaskCanceledException) { }
        };

        flyout.Content = slider;
        flyout.ShowAt((FrameworkElement)sender);
    }

    #endregion

    #region FlipView / General Scroll Handling

    private async Task ForceFlipViewUpdateAsync()
    {
        if (PagesFlipView == null || PagesFlipView.Items.Count == 0)
            return;

        var backupIndex = PagesFlipView.SelectedIndex;
        PagesFlipView.SelectedIndex = -1;
        await Task.Yield();
        PagesFlipView.SelectedIndex = backupIndex;
    }

    private void ScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        sv.AddHandler(
            PointerReleasedEvent,
            new PointerEventHandler(ScrollViewer_PointerReleased),
            handledEventsToo: true
        );
    }

    private void ScrollViewer_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        UpdateCursor((sender as ScrollViewer)!);

    private void ScrollViewer_PointerExited(object sender, PointerRoutedEventArgs e) =>
        UpdateCursor((sender as ScrollViewer)!);

    private void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var scrollViewer = (sender as ScrollViewer)!;
        var properties = e.GetCurrentPoint(null).Properties;

        if (properties.IsLeftButtonPressed && CanPan(scrollViewer))
        {
            _isDragging = true;
            _lastMousePosition = e.GetCurrentPoint(null).Position;
            _startHorizontalOffset = scrollViewer.HorizontalOffset;
            _startVerticalOffset = scrollViewer.VerticalOffset;

            scrollViewer.CapturePointer(e.Pointer);

            UpdateCursor((sender as ScrollViewer)!);
        }
    }

    private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var scrollViewer = (ScrollViewer)sender;
            var currentPosition = e.GetCurrentPoint(null).Position;

            double deltaX = currentPosition.X - _lastMousePosition.X;
            double deltaY = currentPosition.Y - _lastMousePosition.Y;

            _lastMousePosition = currentPosition;

            double newHorizontalOffset = scrollViewer.HorizontalOffset - deltaX;
            double newVerticalOffset = scrollViewer.VerticalOffset - deltaY;

            scrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, null, true);
        }
    }

    private void ScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var scrollViewer = (sender as ScrollViewer)!;

            _isDragging = false;
            scrollViewer.ReleasePointerCapture(e.Pointer);

            UpdateCursor(scrollViewer);
            if (scrollViewer == WebtoonScrollViewer)
                UpdateCurrentPageIndexFromScroll();
        }
    }

    private void ScrollViewer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;

        var sv = (ScrollViewer)sender;
        double centerX = sv.ViewportWidth / 2;
        double centerY = sv.ViewportHeight / 2;

        float targetZoom = sv.ZoomFactor == 1.0f ? 2.0f : 1.0f;

        double contentCenterX = (centerX + sv.HorizontalOffset) / sv.ZoomFactor;
        double contentCenterY = (centerY + sv.VerticalOffset) / sv.ZoomFactor;

        double newHorizontalOffset = (contentCenterX * targetZoom) - centerX;
        double newVerticalOffset = (contentCenterY * targetZoom) - centerY;

        sv.ChangeView(newHorizontalOffset, newVerticalOffset, targetZoom, true);
    }

    private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender!;
        UpdateCursor(scrollViewer);

        if (scrollViewer == WebtoonScrollViewer && !_isDragging && !e.IsIntermediate)
            UpdateCurrentPageIndexFromScroll();
    }

    private void UpdateCursor(ScrollViewer sv) =>
        ProtectedCursor = CanPan(sv)
            ? _isDragging
                ? InputSystemCursor.Create(InputSystemCursorShape.SizeAll)
                : InputSystemCursor.Create(InputSystemCursorShape.Hand)
            : InputSystemCursor.Create(InputSystemCursorShape.Arrow);

    private bool CanPan(ScrollViewer sv)
    {
        bool canHorizontallyScroll = sv.ScrollableWidth > 0 || sv.ExtentWidth > sv.ViewportWidth;
        bool canVerticallyScroll = sv.ScrollableHeight > 0 || sv.ExtentHeight > sv.ViewportHeight;

        return canHorizontallyScroll || canVerticallyScroll || sv.ZoomFactor > 1.0f;
    }

    #endregion

    #region Image Events

    private void Page_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Image im && im.Tag is ChapterPageItemViewModel vm)
        {
            if (im.Source is BitmapImage bmp && bmp.PixelWidth > 0)
            {
                double oldHeight = vm.WebtoonEffectiveHeight;
                vm.SetMeasuredHeight(bmp.PixelWidth, bmp.PixelHeight);
                double newHeight = vm.WebtoonEffectiveHeight;
                InvalidateOffsetsCache();

                if (Math.Abs(newHeight - oldHeight) > 0.5)
                    CompensateScrollForHeightChange(vm, oldHeight, newHeight);
            }
            vm.OnLoadSuccess();
        }
    }

    private void Page_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is Image im && im.Tag is ChapterPageItemViewModel vm)
            vm.OnLoadFailed();
    }

    #endregion

    #region Webtoon Mode

    private void UpdateVisibleWindow(int centerIndex)
    {
        var allPages = ViewModel.ChapterPages;
        if (allPages == null)
            return;

        int newStart = Math.Max(0, centerIndex - DefaultWindowRadius);
        int newEnd = Math.Min(allPages.Count - 1, centerIndex + DefaultWindowRadius);

        if (
            VisibleWebtoonPages.Count == 0
            || centerIndex < _windowStartIndex
            || centerIndex > _windowEndIndex
            || Math.Abs(centerIndex - (_windowStartIndex + _windowEndIndex) / 2)
                > DefaultWindowRadius * 2
        )
        {
            // Clear and rebuild the window to avoid unnecessary add/remove
            // operations when the center index is far from the current window
            VisibleWebtoonPages.Clear();
            for (int i = newStart; i <= newEnd; i++)
            {
                allPages[i].RefreshCacheState();
                VisibleWebtoonPages.Add(allPages[i]);
            }
            _windowStartIndex = newStart;
            _windowEndIndex = newEnd;
        }
        else
        {
            // Remove on left
            while (_windowStartIndex < newStart && VisibleWebtoonPages.Count > 0)
            {
                VisibleWebtoonPages.RemoveAt(0);
                _windowStartIndex++;
            }
            // Add on left
            while (_windowStartIndex > newStart)
            {
                _windowStartIndex--;
                allPages[_windowStartIndex].RefreshCacheState();
                VisibleWebtoonPages.Insert(0, allPages[_windowStartIndex]);
            }
            // Remove on right
            while (_windowEndIndex > newEnd && VisibleWebtoonPages.Count > 0)
            {
                VisibleWebtoonPages.RemoveAt(VisibleWebtoonPages.Count - 1);
                _windowEndIndex--;
            }
            // Add on right
            while (_windowEndIndex < newEnd)
            {
                _windowEndIndex++;
                allPages[_windowEndIndex].RefreshCacheState();
                VisibleWebtoonPages.Add(allPages[_windowEndIndex]);
            }
        }

        TopSpacer.Height = GetWindowTopOffset();
        BottomSpacer.Height = GetWindowBottomOffset();
    }

    private void ClearVisibleWebtoonPages() => VisibleWebtoonPages.Clear();

    private void CompensateScrollForHeightChange(
        ChapterPageItemViewModel vm,
        double oldHeight,
        double newHeight
    )
    {
        int index = ViewModel.ChapterPages?.IndexOf(vm) ?? -1;
        if (index < 0 || index >= ViewModel.WebtoonPageIndex)
            return;

        double delta = newHeight - oldHeight;
        double zoom = WebtoonScrollViewer.ZoomFactor;

        WebtoonScrollViewer.ChangeView(
            null,
            WebtoonScrollViewer.VerticalOffset + delta * zoom,
            null,
            true
        );
    }

    private double GetWindowTopOffset()
    {
        if (ViewModel.ChapterPages == null)
            return 0;
        var offsets = GetCumulativeOffsets();
        return _windowStartIndex < offsets.Length ? offsets[_windowStartIndex] : 0;
    }

    private double GetWindowBottomOffset()
    {
        if (ViewModel.ChapterPages == null)
            return 0;
        var offsets = GetCumulativeOffsets();
        double totalHeight = offsets[^1];
        double usedHeight =
            _windowEndIndex + 1 < offsets.Length ? offsets[_windowEndIndex + 1] : totalHeight;
        return totalHeight - usedHeight;
    }

    private double[] GetCumulativeOffsets()
    {
        if (_cachedOffsets != null)
            return _cachedOffsets;
        var pages = ViewModel.ChapterPages!;
        var offsets = new double[pages.Count + 1];
        for (int i = 0; i < pages.Count; i++)
            offsets[i + 1] = offsets[i] + pages[i].WebtoonEffectiveHeight;
        return _cachedOffsets = offsets;
    }

    private void InvalidateOffsetsCache() => _cachedOffsets = null;

    private void UpdateCurrentPageIndexFromScroll()
    {
        if (_isScrollingToTarget)
            return;

        var offsets = GetCumulativeOffsets();
        float zoom = WebtoonScrollViewer.ZoomFactor;

        double currentLogicalOffset = WebtoonScrollViewer.VerticalOffset / zoom;

        int windowTopOffset = (int)GetWindowTopOffset();
        int windowBottomOffset = (int)(offsets[^1] - GetWindowBottomOffset());
        bool outsideWindow =
            currentLogicalOffset < windowTopOffset - 5
            || currentLogicalOffset > windowBottomOffset + 5;

        if (outsideWindow)
        {
            int idx = Array.BinarySearch(offsets, currentLogicalOffset);
            if (idx < 0)
                idx = ~idx - 1;
            idx = Math.Clamp(idx, 0, ViewModel.ChapterPages!.Count - 1);

            ViewModel.WebtoonPageIndex = idx;

            UpdateVisibleWindow(idx);
            return;
        }

        if (VisibleWebtoonPages is not { Count: > 0 })
            return;

        var viewportRect = new Rect(
            0,
            0,
            WebtoonScrollViewer.ActualWidth,
            WebtoonScrollViewer.ActualHeight
        );
        double viewportCenterY = viewportRect.Height / 2;

        int bestIndex = -1;
        double minDistance = double.MaxValue;

        foreach (var item in WebtoonPageItemsControl.Items)
        {
            if (item is not ChapterPageItemViewModel pageVm)
                continue;
            if (WebtoonPageItemsControl.ContainerFromItem(item) is not FrameworkElement container)
                continue;

            var transform = container.TransformToVisual(WebtoonScrollViewer);
            var bounds = transform.TransformBounds(
                new Rect(0, 0, container.ActualWidth, container.ActualHeight)
            );
            double itemCenterY = bounds.Top + bounds.Height / 2;

            double distance = Math.Abs(itemCenterY - viewportCenterY);
            if (distance < minDistance)
            {
                minDistance = distance;
                int realIndex = ViewModel.ChapterPages!.IndexOf(pageVm);
                if (realIndex >= 0)
                    bestIndex = realIndex;
            }
        }

        if (bestIndex < 0)
            return;

        if (bestIndex != ViewModel.WebtoonPageIndex)
            ViewModel.WebtoonPageIndex = bestIndex;

        if (bestIndex < _windowStartIndex + 2 || bestIndex > _windowEndIndex - 2)
            UpdateVisibleWindow(bestIndex);
    }

    private async void ScrollToPageIndex(int targetIndex, bool isInitialResume = false)
    {
        _positioningOverlayCts?.Cancel();
        _positioningOverlayCts = new CancellationTokenSource();
        var overlayToken = _positioningOverlayCts.Token;

        _ = ShowOverlayIfSlowAsync(overlayToken);

        int resolvedIndex = targetIndex;

        try
        {
            _isScrollingToTarget = true;
            WebtoonScrollViewer.UpdateLayout();

            UpdateVisibleWindow(targetIndex);
            resolvedIndex = await ScrollToOffsetForIndexAsync(targetIndex);

            if (ViewModel.ChapterPages != null)
            {
                var measureRange = ViewModel
                    .ChapterPages.Skip(Math.Max(0, targetIndex - DefaultWindowRadius))
                    .Take((DefaultWindowRadius * 2) + 1)
                    .Where(p => p.MeasuredHeight == null);

                await Task.WhenAll(measureRange.Select(p => p.EnsureMeasuredAsync()));
                InvalidateOffsetsCache();

                if (isInitialResume)
                {
                    // For some reason, the ScrollViewer doesn't always scroll to the correct
                    // position on initial resume, so we force it to scroll to the top first.
                    WebtoonScrollViewer.ChangeView(null, 1, null, true);
                    await Task.Yield();
                }

                resolvedIndex = await ScrollToOffsetForIndexAsync(targetIndex);
            }
        }
        finally
        {
            _positioningOverlayCts?.Cancel();
            ViewModel.IsPositioningWebtoonScroll = false;

            _isScrollingToTarget = false;
            if (ViewModel.WebtoonPageIndex != resolvedIndex)
                ViewModel.WebtoonPageIndex = resolvedIndex;
        }
    }

    private Task<int> ScrollToOffsetForIndexAsync(int targetIndex)
    {
        var tcs = new TaskCompletionSource<int>();
        EventHandler<object>? layoutHandler = null;
        layoutHandler = (_, _) =>
        {
            WebtoonScrollViewer.LayoutUpdated -= layoutHandler;
            var offsets = GetCumulativeOffsets();
            var clamped = Math.Clamp(targetIndex, 0, offsets.Length - 2);
            float zoom = WebtoonScrollViewer.ZoomFactor;
            WebtoonScrollViewer.ChangeView(null, offsets[clamped] * zoom, null, true);
            tcs.SetResult(clamped);
        };
        WebtoonScrollViewer.LayoutUpdated += layoutHandler;

        WebtoonScrollViewer.UpdateLayout();
        return tcs.Task;
    }

    private async Task ShowOverlayIfSlowAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct);
            if (!ct.IsCancellationRequested)
                ViewModel.IsPositioningWebtoonScroll = true;
        }
        catch (TaskCanceledException) { }
    }

    #endregion
}
