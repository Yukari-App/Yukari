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
    public partial class MainPageViewModel : ObservableObject,
        IRecipient<NavigateMessage>, IRecipient<RequestFiltersDialogMessage>, IRecipient<SetSearchTextMessage>
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _searchText = String.Empty;

        public bool IsBackEnabled => _navigationService.CanGoBack;
        public bool IsSearchEnabled => _navigationService.CurrentPage is AppPage.DiscoverPage or AppPage.FavoritesPage;

        public MainPageViewModel(INavigationService navigationService, IDialogService dialogService, IMessenger messenger)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _messenger = messenger;

            _messenger.RegisterAll(this);
        }

        public void Receive(NavigateMessage message) =>
            OnNavigate(message);

        public async void Receive(RequestFiltersDialogMessage message) =>
            await OnFiltersDialogRequested(message);

        public void Receive(SetSearchTextMessage message) =>
            SearchText = message.SearchText ?? string.Empty;

        [RelayCommand]
        private void OnNavigate(NavigateMessage request)
        {
            if (request.PageType == null) return;

            _navigationService.Navigate(request.PageType, request.Parameter);
            OnPropertyChanged(nameof(IsBackEnabled));

            RefreshSearchBox();
        }

        [RelayCommand]
        private void OnBack()
        {
            if (_navigationService.GoBack())
                OnPropertyChanged(nameof(IsBackEnabled));

            RefreshSearchBox();
        }

        [RelayCommand]
        private async Task OnFiltersDialogRequested(RequestFiltersDialogMessage request)
        {
            var selectedFilters = await _dialogService.ShowFiltersDialogAsync(request.Filters, request.AppliedFilters);

            if (selectedFilters != null)
                _messenger.Send(new FiltersDialogResultMessage(selectedFilters));
        }

        [RelayCommand]
        private void OnSearchTextChanged() =>
            _messenger.Send(new SearchChangedMessage(SearchText));

        private void RefreshSearchBox()
        {
            if (_navigationService.CurrentPage != AppPage.DiscoverPage) SearchText = string.Empty;
            OnPropertyChanged(nameof(IsSearchEnabled));
        }
    }
}