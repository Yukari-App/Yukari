using System;
using Microsoft.UI.Xaml.Controls;
using Yukari.Enums;
using Yukari.Services.UI;
using Yukari.Views.Pages;

namespace Yukari.Services;

internal sealed class NavigationService : INavigationService
{
    private Frame? _frame;

    public AppPage CurrentPage =>
        _frame == null
            ? AppPage.Other
            : _frame.CurrentSourcePageType switch
            {
                Type t when t == typeof(FavoritesPage) => AppPage.FavoritesPage,
                Type t when t == typeof(DiscoverPage) => AppPage.DiscoverPage,
                Type t when t == typeof(ComicPage) => AppPage.ComicPage,
                _ => AppPage.Other,
            };

    public void Initialize(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public bool CanGoBack => _frame != null && _frame.CanGoBack;

    public void Navigate(Type pageType, object? parameter = null)
    {
        if (_frame == null)
            throw new InvalidOperationException(
                "NavigationService not initialized. Call Initialize(Frame) before navigating."
            );

        if (_frame.CurrentSourcePageType != pageType)
            _frame.Navigate(pageType, parameter);
    }

    public bool GoBack()
    {
        if (!CanGoBack)
            return false;

        _frame!.GoBack();
        return true;
    }
}
