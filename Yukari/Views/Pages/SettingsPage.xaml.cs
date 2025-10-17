using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            DataContext = App.GetService<SettingsPageViewModel>();
        }
    }
}
