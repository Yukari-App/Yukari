using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class DiscoverPage : Page
    {
        public DiscoverPage()
        {
            InitializeComponent();

            DataContext = App.GetService<DiscoverPageViewModel>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.New && DataContext is DiscoverPageViewModel viewModel)
                await viewModel.InitializeAsync();   
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (DataContext is DiscoverPageViewModel viewModel && e.ClickedItem is ComicItemViewModel comicItem)
                viewModel.NavigateToComicCommand.Execute(comicItem.Identifier);
        }
    }
}
