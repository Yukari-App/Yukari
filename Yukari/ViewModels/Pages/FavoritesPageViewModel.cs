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
    public partial class FavoritesPageViewModel : ObservableObject, IRecipient<SearchMessage>
    {
        private IComicService _comicService;

        [ObservableProperty] private List<ComicItemViewModel> _favoriteComics = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NoFavorites))]
        private bool _isContentLoading = true;

        public bool NoFavorites => !IsContentLoading && FavoriteComics.Count == 0;

        public FavoritesPageViewModel(IComicService comicService)
        {
            WeakReferenceMessenger.Default.Register<SearchMessage>(this);

            _comicService = comicService;
        }

        public async void Receive(SearchMessage message) => await UpdateDisplayedComicsAsync(message.SearchText);

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
            WeakReferenceMessenger.Default.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), comicIdentifier));
    }
}
