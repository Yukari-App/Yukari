using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Services;

namespace Yukari.ViewModels
{
    internal partial class FavoritesPageViewModel : ObservableObject, IRecipient<SearchMessage>
    {
        private IComicService _comicService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NoFavorites))]
        private ObservableCollection<ComicItemViewModel> _favoriteComics = new();

        [ObservableProperty] private bool _isContentLoading = true;

        public bool NoFavorites => !FavoriteComics.Any();

        public FavoritesPageViewModel(IComicService comicService)
        {
            WeakReferenceMessenger.Default.Register<SearchMessage>(this);

            _comicService = comicService;
            _ = UpdateDisplayedComics();
        }

        public void Receive(SearchMessage message)
        {
            _ = UpdateDisplayedComics(message.SearchText);
        }

        private async Task UpdateDisplayedComics(string? searchText = null)
        {
            FavoriteComics.Clear();

            IsContentLoading = true;
            var comics = await _comicService.SearchComicsAsync("MangaDex", searchText, new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>());

            FavoriteComics = new ObservableCollection<ComicItemViewModel>(
               comics.Select(comic => new ComicItemViewModel(comic, _comicService))
            );
            IsContentLoading = false;
        }

        [RelayCommand]
        private void NavigateToComic(ContentIdentifier comicIdentifier)
        {
            WeakReferenceMessenger.Default.Send(new NavigateMessage(typeof(Views.ComicPage), comicIdentifier));
        }
    }
}
