using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Yukari.Models.DTO;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class ReaderPage : Page
    {
        private Point _lastMousePosition;
        private double _startHorizontalOffset;
        private double _startVerticalOffset;
        private bool _isDragging = false;

        public ReaderPage()
        {
            InitializeComponent();

            DataContext = App.GetService<ReaderPageViewModel>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ReaderNavigationArgs args)
            {
                if (DataContext is ReaderPageViewModel viewModel)
                    await viewModel.InitializeAsync(args.ComicKey, args.ComicTitle, args.ChapterKey, args.SelectedLang);
            }
        }

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

            if (properties.IsLeftButtonPressed && pageScrollViewer.ZoomFactor > 1.0f)
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

                pageScrollViewer.ChangeView(_startHorizontalOffset - deltaX, _startVerticalOffset - deltaY, null, true);
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
            if (sv.ZoomFactor > 1.0f)
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
}
