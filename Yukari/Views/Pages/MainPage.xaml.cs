using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using Yukari.Messages;
using Yukari.Services.UI;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            App.GetService<INavigationService>().Initialize(ContentFrame);

            var viewModel = App.GetService<MainPageViewModel>();

            DataContext = viewModel;
            viewModel.NavigateCommand.Execute(new NavigateMessage(typeof(FavoritesPage), null));
        }

        private void NavigationViewControl_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var tag = args.IsSettingsInvoked ? "Yukari.Views.Pages.SettingsPage" : args.InvokedItemContainer?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tag))
                ((MainPageViewModel)DataContext).NavigateCommand.Execute(new NavigateMessage(Type.GetType(tag), null));
        }

        private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            ((MainPageViewModel)DataContext).BackCommand.Execute(null);
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (ContentFrame.SourcePageType != null && ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
                return;
            }

            var selectedMenuItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>()
                .Concat(NavigationViewControl.FooterMenuItems.OfType<NavigationViewItem>())
                .FirstOrDefault(item => item.Tag?.ToString() == ContentFrame.SourcePageType.FullName);

            if (selectedMenuItem != null)
                NavigationViewControl.SelectedItem = selectedMenuItem;
        }

        private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args) =>
            AppTitleBar.Margin = args.DisplayMode == NavigationViewDisplayMode.Minimal ? new Thickness { Left = 96 } : new Thickness { Left = 48 };

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => 
            ((MainPageViewModel)DataContext).SearchTextChangedCommand.Execute(args.Reason);
    }
}