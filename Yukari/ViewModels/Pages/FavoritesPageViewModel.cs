using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Messages;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages;

public partial class FavoritesPageViewModel : ObservableObject, IRecipient<SearchChangedMessage>
{
    private readonly IComicService _comicService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IMessenger _messenger;

    private CancellationTokenSource _navigationCts = new();
    private CancellationTokenSource _searchCts = new();

    [ObservableProperty]
    public partial List<ComicItemViewModel> FavoriteComics { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoCollections))]
    public partial string[] Collections { get; set; } = Array.Empty<string>();

    [ObservableProperty]
    public partial string? SelectedCollection { get; set; }

    [ObservableProperty]
    public partial bool ShowAllCollections { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoFavorites))]
    public partial bool IsContentLoading { get; set; } = true;

    public bool NoFavorites => !IsContentLoading && FavoriteComics.Count == 0;
    public bool NoCollections => Collections.Length == 0;

    public FavoritesPageViewModel(
        IComicService comicService,
        IDialogService dialogService,
        INotificationService notificationService,
        IMessenger messenger
    )
    {
        _comicService = comicService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _messenger = messenger;

        _messenger.RegisterAll(this);
    }

    public void Receive(SearchChangedMessage message) =>
        _ = UpdateDisplayedComicsAsync(message.SearchText);

    public async Task InitializeAsync()
    {
        await UpdateCollectionsAsync();
        await UpdateDisplayedComicsAsync();
    }

    public void OnNavigatedFrom()
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();
    }

    [RelayCommand]
    private void ToggleShowAllCollections()
    {
        if (ShowAllCollections)
        {
            SelectedCollection = null;
            return;
        }

        SelectedCollection = Collections.FirstOrDefault();
    }

    [RelayCommand]
    private async Task OpenCollectionManagerAsync()
    {
        await _dialogService.ShowCollectionsManagerAsync();

        await UpdateCollectionsAsync();
        await UpdateDisplayedComicsAsync();
    }

    [RelayCommand]
    private void NavigateToComic(ContentKey comicKey) =>
        _messenger.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), comicKey));

    [RelayCommand]
    private async Task RemoveFavoriteComicAsync(ContentKey comicKey)
    {
        var result = await _comicService.RemoveFavoriteComicAsync(comicKey);
        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        await UpdateDisplayedComicsAsync();
    }

    [RelayCommand]
    private async Task OpenComicCollectionsManagerAsync(ContentKey comicKey) =>
        throw new NotImplementedException();

    private async Task UpdateDisplayedComicsAsync(string? searchText = null)
    {
        _searchCts.Cancel();
        _searchCts.Dispose();
        _searchCts = new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _searchCts.Token,
            _navigationCts.Token
        );

        IsContentLoading = true;

        FavoriteComics = new List<ComicItemViewModel>();
        var result = await _comicService.GetFavoriteComicsAsync(
            searchText,
            SelectedCollection,
            linkedCts.Token
        );

        if (result.IsCancelled)
            return;

        if (result.IsSuccess)
            FavoriteComics = result
                .Value!.Select(comic => new ComicItemViewModel(
                    comic,
                    RemoveFavoriteComicCommand,
                    OpenComicCollectionsManagerCommand
                ))
                .ToList();
        else
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);

        IsContentLoading = false;
    }

    private async Task UpdateCollectionsAsync()
    {
        var result = await _comicService.GetCollectionsAsync(_navigationCts.Token);
        if (result.IsSuccess)
            Collections = result.Value!.ToArray();
    }

    async partial void OnSelectedCollectionChanged(string? value)
    {
        ShowAllCollections = value == null;
        await UpdateDisplayedComicsAsync();
    }
}
