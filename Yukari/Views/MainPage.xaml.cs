using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
using Yukari.ViewModels;

namespace Yukari.Views
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            var navigationService = (App.Current as App).NavigationService;
            navigationService.Initialize(ContentFrame);

            DataContext = (App.Current as App).Services.GetService<MainPageViewModel>();
        }

        private void NavigationViewControl_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var tag = args.IsSettingsInvoked ? "Yukari.Views.SettingsPage" : args.InvokedItemContainer?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tag))
                (DataContext as MainPageViewModel).NavigateCommand.Execute(tag);
        }

        private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            (DataContext as MainPageViewModel).BackCommand.Execute(null);
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.SettingsItem;
            }
            else if (ContentFrame.SourcePageType != null)
            {
                NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>()
                    .Concat(NavigationViewControl.FooterMenuItems.OfType<NavigationViewItem>())
                    .First(n => n.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));
            }

            NavigationViewControl.Header = ((NavigationViewItem)NavigationViewControl.SelectedItem)?.Content?.ToString();
        }

        private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.Margin = new Thickness { Left = 96 };
            }
            else
            {
                AppTitleBar.Margin = new Thickness { Left = 48 };
            }
        }
    }
}