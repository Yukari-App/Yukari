using Microsoft.UI.Xaml.Controls;
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
    }
}