using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly INavigationService _nav;

        public MainPageViewModel(INavigationService navService)
        {
            _nav = navService;

            NavigateCommand = new RelayCommand<object>(OnNavigate);
            BackCommand = new RelayCommand(OnBack, () => _nav.CanGoBack);

            // inicializa estado
            IsBackEnabled = _nav.CanGoBack;
        }

        [ObservableProperty] private bool _isBackEnabled;

        // --- Commands ---
        public ICommand NavigateCommand { get; }
        public ICommand BackCommand { get; }

        // --- Métodos de Command ---
        private void OnNavigate(object param)
        {
            var pageTypeName = param as string;
            if (pageTypeName is null) return;

            var pageType = Type.GetType(pageTypeName);
            _nav.Navigate(pageType);
            IsBackEnabled = _nav.CanGoBack;
        }

        private void OnBack()
        {
            if (_nav.GoBack())
                IsBackEnabled = _nav.CanGoBack;
        }
    }

}