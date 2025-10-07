using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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

            if (e.Parameter is string comicId)
            {
                var viewModel = ((App)App.Current).Services.GetRequiredService<ComicPageViewModel>();
                await viewModel.InitializeAsync(comicId);

                DataContext = viewModel;
            }
        }
    }
}