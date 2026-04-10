using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPageViewModel ViewModel { get; set; }

        public SettingsPage()
        {
            InitializeComponent();

            ViewModel = App.GetService<SettingsPageViewModel>();
            DataContext = ViewModel;
        }
    }
}
