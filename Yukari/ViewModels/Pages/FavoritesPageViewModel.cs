using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Services.Comics;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class FavoritesPageViewModel : ObservableObject, IRecipient<SearchChangedMessage>
    {
        private readonly IComicService _comicService;
        private readonly IMessenger _messenger;

        [ObservableProperty] private List<ComicItemViewModel> _favoriteComics = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NoFavorites))]
        private bool _isContentLoading = true;

        public bool NoFavorites => !IsContentLoading && FavoriteComics.Count == 0;

        public FavoritesPageViewModel(IComicService comicService, IMessenger messenger)
        {
            _comicService = comicService;
            _messenger = messenger;

            _messenger.RegisterAll(this);
        }

        public async void Receive(SearchChangedMessage message) => await UpdateDisplayedComicsAsync(message.SearchText);

        public async Task InitializeAsync() => await UpdateDisplayedComicsAsync();

        private async Task UpdateDisplayedComicsAsync(string? searchText = null)
        {
            IsContentLoading = true;

            FavoriteComics = (await _comicService.GetFavoriteComicsAsync(searchText, "all"))
                .Select(comic => new ComicItemViewModel(comic, _comicService)).ToList();

            IsContentLoading = false;
        }

        [RelayCommand]
        private void NavigateToComic(ContentIdentifier comicIdentifier) =>
            _messenger.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), comicIdentifier));
    }
}
