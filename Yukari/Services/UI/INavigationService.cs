using Microsoft.UI.Xaml.Controls;
using System;
using Yukari.Enums;

namespace Yukari.Services.UI
{
    public interface INavigationService
    {
        AppPage CurrentPage { get; }
        void Initialize(Frame frame);
        bool CanGoBack { get; }
        void Navigate(Type pageType, object? parameter = null);
        bool GoBack();
    }
}
