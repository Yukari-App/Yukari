using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels;

namespace Yukari.Views.Pages
{
    public sealed partial class DownloadsPage : Page
    {
        public DownloadsPage()
        {
            InitializeComponent();
            DataContext = App.GetService<DownloadsPageViewModel>();
        }
    }
}
