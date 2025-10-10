using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Models;
using Yukari.ViewModels;

namespace Yukari.Views
{
    public sealed partial class ComicPage : Page
    {
        public ComicPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ContentIdentifier comicIdentifier)
            {
                var viewModel = ((App)App.Current).Services.GetRequiredService<ComicPageViewModel>();
                DataContext = viewModel;
                await viewModel.InitializeAsync(comicIdentifier);
            }
        }
    }
}