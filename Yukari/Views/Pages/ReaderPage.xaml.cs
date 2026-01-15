using Microsoft.UI.Xaml.Controls;
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
    }
}
