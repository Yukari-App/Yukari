using System;

namespace Yukari.Services
{
    public interface INavigationService
    {
        bool CanGoBack { get; }
        void Navigate(Type pageType, object parameter = null);
        bool GoBack();
        event EventHandler Navigated;
    }

}
