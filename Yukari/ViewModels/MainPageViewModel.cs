using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;
using Yukari.Models;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly INavigationService _nav;

        public MainPageViewModel(INavigationService navService)
        {
            _nav = navService;

            NavigateCommand = new RelayCommand<NavigationRequest>(OnNavigate);
            BackCommand = new RelayCommand(OnBack, () => _nav.CanGoBack);
            
            IsBackEnabled = _nav.CanGoBack;
        }

        [ObservableProperty] private bool _isBackEnabled;

        public ICommand NavigateCommand { get; }
        public ICommand BackCommand { get; }

        private void OnNavigate(NavigationRequest request)
        {
            if (String.IsNullOrEmpty(request.PageTypeName)) return;

            _nav.Navigate(Type.GetType(request.PageTypeName), request.Parameter);
            IsBackEnabled = _nav.CanGoBack;
        }

        private void OnBack()
        {
            if (_nav.GoBack())
                IsBackEnabled = _nav.CanGoBack;
        }
    }
}