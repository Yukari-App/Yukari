using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Models.DTO;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class ComicPage : Page
    {
        public ComicPage()
        {
            InitializeComponent();
            DataContext = App.GetService<ComicPageViewModel>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ContentKey ComicKey)
            {
                if (DataContext is ComicPageViewModel viewModel)
                    await viewModel.InitializeAsync(ComicKey);
            }
        }

        private void ChapterItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ComicPageViewModel viewModel && sender is Button b)
                viewModel.NavigateToReaderCommand.Execute(b.CommandParameter);
        }

        private void ChapterToggleRead_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ComicPageViewModel viewModel && sender is ToggleButton b)
                viewModel.ChapterToggleReadCommand.Execute(b.CommandParameter);
        }

        private void MarkPreviousChaptersAsRead_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ComicPageViewModel viewModel && sender is MenuFlyoutItem b)
                viewModel.MarkPreviousChaptersAsReadCommand.Execute(b.CommandParameter);
        }
    }
}