using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels;

namespace Yukari.Views
{
    public sealed partial class FavoritesPage : Page
    {
        public FavoritesPage()
        {
            this.InitializeComponent();

            var viewModel = ((App)App.Current).Services.GetRequiredService<FavoritesPageViewModel>();
            this.DataContext = viewModel;
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e) =>
            ((FavoritesPageViewModel)DataContext).NavigateToMangaCommand.Execute(((MangaItemViewModel)e.ClickedItem).Id);
    }
}
