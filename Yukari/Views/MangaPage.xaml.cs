using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Yukari.ViewModels;

namespace Yukari.Views
{
    public sealed partial class MangaPage : Page
    {
        public MangaPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Guid mangaId)
            {
                var viewModel = ((App)App.Current).Services.GetRequiredService<MangaPageViewModel>();
                await viewModel.InitializeAsync(mangaId);

                DataContext = viewModel;
            }
        }
    }
}