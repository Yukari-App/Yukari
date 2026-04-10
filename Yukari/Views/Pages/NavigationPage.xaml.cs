using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Messages;
using Yukari.Services.UI;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class NavigationPage : Page
    {
        public NavigationPageViewModel ViewModel { get; set; }

        public NavigationPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<NavigationPageViewModel>();
            DataContext = ViewModel;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            App.GetService<INavigationService>().Initialize(ContentFrame);
            App.GetService<IDialogService>().Initialize(XamlRoot);

            if (ContentFrame.Content == null)
                ViewModel.NavigateCommand.Execute(new NavigateMessage(typeof(FavoritesPage), null));
        }

        private void NavigationViewControl_ItemInvoked(
            NavigationView sender,
            NavigationViewItemInvokedEventArgs args
        )
        {
            var tag = args.IsSettingsInvoked
                ? "Yukari.Views.Pages.SettingsPage"
                : args.InvokedItemContainer?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tag))
                ViewModel.NavigateCommand.Execute(new NavigateMessage(Type.GetType(tag), null));
        }

        private void NavigationViewControl_BackRequested(
            NavigationView sender,
            NavigationViewBackRequestedEventArgs args
        ) => ViewModel.BackCommand.Execute(null);

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
                return;
            }

            var selectedMenuItem = NavigationViewControl
                .MenuItems.OfType<NavigationViewItem>()
                .Concat(NavigationViewControl.FooterMenuItems.OfType<NavigationViewItem>())
                .FirstOrDefault(item =>
                    item.Tag.ToString() == ContentFrame.SourcePageType.FullName
                );

            if (selectedMenuItem != null)
                NavigationViewControl.SelectedItem = selectedMenuItem;
        }

        private void NavigationViewControl_DisplayModeChanged(
            NavigationView sender,
            NavigationViewDisplayModeChangedEventArgs args
        ) =>
            AppTitleBar.Margin =
                args.DisplayMode == NavigationViewDisplayMode.Minimal
                    ? new Thickness { Left = 96 }
                    : new Thickness { Left = 48 };

        private void AutoSuggestBox_TextChanged(
            AutoSuggestBox sender,
            AutoSuggestBoxTextChangedEventArgs args
        )
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
                ViewModel.SearchTextChangedCommand.Execute(null);
        }
    }
}
