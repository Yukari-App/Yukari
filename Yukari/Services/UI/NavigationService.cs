using Microsoft.UI.Xaml.Controls;
using System;
using Yukari.Services.UI;

namespace Yukari.Services
{
    internal class NavigationService : INavigationService
    {
        private Frame _frame;
        public Type? CurrentPageType => _frame.CurrentSourcePageType;

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
