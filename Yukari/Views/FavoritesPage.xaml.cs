using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels;

namespace Yukari.Views
{
    public sealed partial class FavoritesPage : Page
    {
        public FavoritesPage()
        {
            InitializeComponent();
            DataContext = App.GetService<FavoritesPageViewModel>(); ;
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e) =>
            ((FavoritesPageViewModel)DataContext).NavigateToComicCommand.Execute(((ComicItemViewModel)e.ClickedItem).Identifier);
    }
}
