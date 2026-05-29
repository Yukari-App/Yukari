using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Services.Settings;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Pages;

public partial class NavigationPageViewModel
    : ObservableObject,
        IRecipient<NavigateMessage>,
        IRecipient<SetSearchTextMessage>
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IMessenger _messenger;

    private bool _isInitialized;

    private CancellationTokenSource? _cts;

    [ObservableProperty]
    public partial bool IsNavigationPaneOpen { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = String.Empty;

    public bool IsBackEnabled => _navigationService.CanGoBack;
    public bool IsSearchEnabled =>
        _navigationService.CurrentPage is AppPage.DiscoverPage or AppPage.FavoritesPage;

    public NavigationPageViewModel(
        INavigationService navigationService,
        ISettingsService settingsService,
        IMessenger messenger
    )
    {
        _navigationService = navigationService;
        _settingsService = settingsService;
        _messenger = messenger;

        _messenger.RegisterAll(this);
        IsNavigationPaneOpen = _settingsService.Current.NavigationPaneIsOpen;

        _isInitialized = true;
    }

    public void Receive(NavigateMessage message) => OnNavigate(message);

    public void Receive(SetSearchTextMessage message) =>
        SearchText = message.SearchText ?? string.Empty;

    [RelayCommand]
    private void OnNavigate(NavigateMessage request)
    {
        if (request.PageType == null)
            return;

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
    private async Task OnSearchTextChanged()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            await Task.Delay(400, _cts.Token);
            _messenger.Send(new SearchChangedMessage(SearchText));
        }
        catch (TaskCanceledException) { }
    }

    private void RefreshSearchBox()
    {
        if (_navigationService.CurrentPage != AppPage.DiscoverPage)
            SearchText = string.Empty;
        OnPropertyChanged(nameof(IsSearchEnabled));
    }

    async partial void OnIsNavigationPaneOpenChanged(bool value)
    {
        if (_isInitialized)
            _settingsService.Set(s => s.NavigationPaneIsOpen, value);
    }
}
