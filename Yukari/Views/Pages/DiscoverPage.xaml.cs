using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class DiscoverPage : Page
    {
        public DiscoverPageViewModel ViewModel { get; set; }

        public DiscoverPage()
        {
            InitializeComponent();

            ViewModel = App.GetService<DiscoverPageViewModel>();
            DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.OnNavigatedTo();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.OnNavigatedFrom();
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ComicItemViewModel comicItem)
                ViewModel.NavigateToComicCommand.Execute(comicItem.Key);
        }
    }
}
