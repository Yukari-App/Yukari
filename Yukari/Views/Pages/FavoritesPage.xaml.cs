using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class FavoritesPage : Page
    {
        public FavoritesPage()
        {
            InitializeComponent();
            DataContext = App.GetService<FavoritesPageViewModel>(); ;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (DataContext is FavoritesPageViewModel viewModel)
                await viewModel.InitializeAsync();
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (DataContext is FavoritesPageViewModel viewModel && e.ClickedItem is ComicItemViewModel comicItem)
                viewModel.NavigateToComicCommand.Execute(comicItem.Key);
        }
    }
}
