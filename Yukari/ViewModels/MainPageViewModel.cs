using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Windows.Input;
using Yukari.Messages;
using Yukari.Services;
using Yukari.Views;

namespace Yukari.ViewModels
{
    public partial class MainPageViewModel : ObservableObject, IRecipient<NavigateMessage>
    {
        private readonly INavigationService _nav;

        [ObservableProperty] private bool _isBackEnabled;

        public bool IsSearchEnabled => _nav.CurrentPageType == typeof(FavoritesPage) || _nav.CurrentPageType == typeof(DiscoverPage);

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

            OnPropertyChanged(nameof(IsSearchEnabled));
        }

        public bool CanNavigateBack() =>
            _nav.CanGoBack;

        [RelayCommand(CanExecute = nameof(CanNavigateBack))]
        private void OnBack()
        {
            if (_nav.GoBack())
                IsBackEnabled = _nav.CanGoBack;

            OnPropertyChanged(nameof(IsSearchEnabled));
        }
    }
}