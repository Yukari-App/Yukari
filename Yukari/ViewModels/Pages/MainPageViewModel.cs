using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Pages
{
    public partial class MainPageViewModel : ObservableObject, IRecipient<NavigateMessage>, IRecipient<RequestFiltersDialogMessage>
    {
        private readonly INavigationService _nav;
        private readonly IDialogService _dialogService;

        [ObservableProperty] private bool _isBackEnabled;

        public bool IsSearchEnabled => _nav.CurrentPage is Enums.AppPage.DiscoverPage or Enums.AppPage.FavoritesPage;

        [ObservableProperty]
        private string _searchText = String.Empty;

        public MainPageViewModel(INavigationService navService, IDialogService dialogService)
        {
            _nav = navService;
            _dialogService = dialogService;

            WeakReferenceMessenger.Default.Register<NavigateMessage>(this);
            WeakReferenceMessenger.Default.Register<RequestFiltersDialogMessage>(this);

            IsBackEnabled = _nav.CanGoBack;
        }

        public void Receive(NavigateMessage message) =>
            OnNavigate(message);

        public async void Receive(RequestFiltersDialogMessage message) =>
            await OnFiltersDialogRequested(message);

        [RelayCommand]
        private void OnNavigate(NavigateMessage request)
        {
            if (request.PageType == null) return;

            _nav.Navigate(request.PageType, request.Parameter);
            IsBackEnabled = _nav.CanGoBack;

            InitializeSearchBox();
        }

        [RelayCommand]
        private void OnBack()
        {
            if (_nav.GoBack())
                IsBackEnabled = _nav.CanGoBack;

            InitializeSearchBox();
        }

        [RelayCommand]
        private async Task OnFiltersDialogRequested(RequestFiltersDialogMessage request)
        {
            var selectedFilters = await _dialogService.ShowFiltersDialogAsync(request.Filters, request.AppliedFilters);

            if (selectedFilters != null)
                WeakReferenceMessenger.Default.Send(new FiltersDialogResultMessage(selectedFilters));
        }

        [RelayCommand]
        private void OnSearchTextChanged() =>
            WeakReferenceMessenger.Default.Send(new SearchMessage(SearchText));

        private void InitializeSearchBox()
        {
            if (_nav.CurrentPage is not (AppPage.FavoritesPage or AppPage.DiscoverPage or AppPage.ComicPage))
                SearchText = string.Empty;

            OnPropertyChanged(nameof(IsSearchEnabled));
        }
    }
}