using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Models;
using Yukari.ViewModels;

namespace Yukari.Views
{
    public sealed partial class FavoritesPage : Page
    {
        public FavoritesPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var viewModel = ((App)App.Current).Services.GetRequiredService<FavoritesPageViewModel>();
            this.DataContext = viewModel;

            await viewModel.LoadFavoriteMangasAsync();
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e) =>
            ((FavoritesPageViewModel)DataContext).NavigateToMangaCommand.Execute(((Manga)e.ClickedItem).Id);
    }
}
