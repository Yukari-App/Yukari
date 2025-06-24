using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Windows.Input;
using Yukari.Messages;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class MainPageViewModel : ObservableObject, IRecipient<NavigateMessage>
    {
        private readonly INavigationService _nav;

        public MainPageViewModel(INavigationService navService)
        {
            _nav = navService;

            WeakReferenceMessenger.Default.Register<NavigateMessage>(this);

            NavigateCommand = new RelayCommand<NavigateMessage>(OnNavigate);
            BackCommand = new RelayCommand(OnBack, () => _nav.CanGoBack);
            
            IsBackEnabled = _nav.CanGoBack;
        }

        [ObservableProperty] private bool _isBackEnabled;

        public ICommand NavigateCommand { get; }
        public ICommand BackCommand { get; }

        public void Receive(NavigateMessage message) => 
            OnNavigate(message);

        private void OnNavigate(NavigateMessage request)
        {
            if (request.PageType == null) return;

            _nav.Navigate(request.PageType, request.Parameter);
            IsBackEnabled = _nav.CanGoBack;
        }

        private void OnBack()
        {
            if (_nav.GoBack())
                IsBackEnabled = _nav.CanGoBack;
        }
    }
}