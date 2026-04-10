using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class DownloadsPage : Page
    {
        public DownloadsPageViewModel ViewModel { get; set; }

        public DownloadsPage()
        {
            InitializeComponent();

            ViewModel = new DownloadsPageViewModel();
            DataContext = ViewModel;
        }
    }
}
