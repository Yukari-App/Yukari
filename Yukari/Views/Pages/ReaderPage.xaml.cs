using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Yukari.Models.DTO;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages;

public sealed partial class ReaderPage : Page
{
    public ReaderPageViewModel ViewModel { get; set; }

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
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReaderPageViewModel.ReadingMode))
        {
            await HandleReadingModeChangedAsync();
        }
    }

    private void ContentSection_PointerExited(object sender, PointerRoutedEventArgs e) =>
        AppTitleBar.Visibility = Visibility.Visible;

    private void ContentSection_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsFullscreen)
            AppTitleBar.Visibility = Visibility.Collapsed;
    }

    private void ContentSection_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Grid grid)
            UpdateScreenSize(grid.ActualWidth, grid.ActualHeight);
    }

    private void ContentSection_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateScreenSize(e.NewSize.Width, e.NewSize.Height);

    private async Task HandleReadingModeChangedAsync()
    {
        if (PagesFlipView == null)
            return;

        var backupIndex = PagesFlipView.SelectedIndex;
        PagesFlipView.SelectedIndex = -1;
        await Task.Yield();
        PagesFlipView.SelectedIndex = backupIndex;
    }

    private void UpdateScreenSize(double width, double height) =>
        ViewModel.SetScreenSizeCommand.Execute((width, height));

    // FlipView ItemTemplate Controls Handlers

    private Point _lastMousePosition;
    private double _startHorizontalOffset;
    private double _startVerticalOffset;
    private bool _isDragging = false;

    private void PageScrollViewer_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        UpdateCursor((sender as ScrollViewer)!);

    private void PageScrollViewer_PointerExited(object sender, PointerRoutedEventArgs e) =>
        UpdateCursor((sender as ScrollViewer)!);

    private void PageScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) =>
        UpdateCursor((sender as ScrollViewer)!);

    private void PageScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pageScrollViewer = (sender as ScrollViewer)!;
        var properties = e.GetCurrentPoint(null).Properties;

        if (properties.IsLeftButtonPressed && CanPan(pageScrollViewer))
        {
            _isDragging = true;
            _lastMousePosition = e.GetCurrentPoint(null).Position;
            _startHorizontalOffset = pageScrollViewer.HorizontalOffset;
            _startVerticalOffset = pageScrollViewer.VerticalOffset;

            pageScrollViewer.CapturePointer(e.Pointer);

            UpdateCursor((sender as ScrollViewer)!);
        }
    }

    private void PageScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var pageScrollViewer = (sender as ScrollViewer)!;
            var currentPosition = e.GetCurrentPoint(null).Position;

            double deltaX = currentPosition.X - _lastMousePosition.X;
            double deltaY = currentPosition.Y - _lastMousePosition.Y;

            pageScrollViewer.ChangeView(
                _startHorizontalOffset - deltaX,
                _startVerticalOffset - deltaY,
                null,
                true
            );
        }
    }

    private void PageScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var pageScrollViewer = (sender as ScrollViewer)!;

            _isDragging = false;
            pageScrollViewer.ReleasePointerCapture(e.Pointer);

            UpdateCursor((sender as ScrollViewer)!);
        }
    }

    private void UpdateCursor(ScrollViewer sv)
    {
        if (CanPan(sv))
        {
            ProtectedCursor = _isDragging
                ? InputSystemCursor.Create(InputSystemCursorShape.SizeAll)
                : InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }
        else
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }
    }

    private bool CanPan(ScrollViewer sv)
    {
        bool canHorizontallyScroll = sv.ScrollableWidth > 0 || sv.ExtentWidth > sv.ViewportWidth;
        bool canVerticallyScroll = sv.ScrollableHeight > 0 || sv.ExtentHeight > sv.ViewportHeight;

        return canHorizontallyScroll || canVerticallyScroll || sv.ZoomFactor > 1.0f;
    }

    private void Page_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Image im && im.DataContext is ChapterPageItemViewModel vm)
            vm.OnLoadSuccess();
    }

    private void Page_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is Image im && im.DataContext is ChapterPageItemViewModel vm)
            vm.OnLoadFailed();
    }
}
