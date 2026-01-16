using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Models.DTO;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class ReaderPage : Page
    {
        public ReaderPage()
        {
            InitializeComponent();

            DataContext = App.GetService<ReaderPageViewModel>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ReaderNavigationArgs args)
            {
                if (DataContext is ReaderPageViewModel viewModel)
                    await viewModel.InitializeAsync(args.ComicKey, args.ComicTitle, args.ChapterKey, args.SelectedLang);
            }
        }
    }
}
