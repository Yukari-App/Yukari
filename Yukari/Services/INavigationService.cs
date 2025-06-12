using Microsoft.UI.Xaml.Controls;
using System;

namespace Yukari.Services
{
    public interface INavigationService
    {
        void Initialize(Frame frame);
        bool CanGoBack { get; }
        void Navigate(Type pageType, object parameter = null);
        bool GoBack();
    }
}
