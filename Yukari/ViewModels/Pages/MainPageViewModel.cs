using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Windows.Input;
using Yukari.Messages;
using Yukari.Services.UI;
using Yukari.Views.Pages;

namespace Yukari.ViewModels.Pages
{
    public partial class MainPageViewModel : ObservableObject, IRecipient<NavigateMessage>
    {
        private readonly INavigationService _nav;

        [ObservableProperty] private bool _isBackEnabled;

        public bool IsSearchEnabled => _nav.CurrentPageType == typeof(FavoritesPage) || _nav.CurrentPageType == typeof(DiscoverPage);

        [ObservableProperty]
        private string _searchText = String.Empty;

        public MainPageViewModel(INavigationService navService)
        {
            _nav = navService;

            WeakReferenceMessenger.Default.Register<NavigateMessage>(this);

            IsBackEnabled = _nav.CanGoBack;
        }

        public void Receive(NavigateMessage message) =>
            OnNavigate(message);

        [RelayCommand]
        private void OnNavigate(NavigateMessage request)
        {
            if (request.PageType == null) return;

            _nav.Navigate(request.PageType, request.Parameter);
            IsBackEnabled = _nav.CanGoBack;

            ResetSearchBox();
        }

        [RelayCommand]
        private void OnBack()
        {
            if (_nav.GoBack())
                IsBackEnabled = _nav.CanGoBack;

            ResetSearchBox();
        }

        [RelayCommand]
        private void OnSearchTextChanged(AutoSuggestionBoxTextChangeReason Reason)
        {
            if (Reason != AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
                WeakReferenceMessenger.Default.Send(new SearchMessage(SearchText));
        }

        private void ResetSearchBox()
        {
            SearchText = string.Empty;
            OnPropertyChanged(nameof(IsSearchEnabled));
        }
    }
}