using Microsoft.UI.Xaml.Controls;
using System;
using Yukari.Enums;
using Yukari.Services.UI;
using Yukari.Views.Pages;

namespace Yukari.Services
{
    internal class NavigationService : INavigationService
    {
        private Frame _frame;

        public AppPage CurrentPage => 
            _frame.CurrentSourcePageType switch
            {
                Type t when t == typeof(FavoritesPage) => AppPage.FavoritesPage,
                Type t when t == typeof(DiscoverPage) => AppPage.DiscoverPage,
                Type t when t == typeof(ComicPage) => AppPage.ComicPage,
                _ => AppPage.Other
            };

        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public bool CanGoBack => _frame.CanGoBack;

        public void Navigate(Type pageType, object parameter = null)
        {
            if (_frame.CurrentSourcePageType != pageType)
                _frame.Navigate(pageType, parameter);
        }

        public bool GoBack()
        {
            if (!CanGoBack) return false;
            _frame.GoBack();
            return true;
        }
    }
}
