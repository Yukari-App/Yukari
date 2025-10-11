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
    public partial class FavoritesPageViewModel : ObservableObject, IRecipient<SearchMessage>
    {
        private IComicService _comicService;

        [ObservableProperty] private ObservableCollection<ComicItemViewModel> _favoriteComics = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NoFavorites))]
        private bool _isContentLoading = true;

        public bool NoFavorites => !IsContentLoading && !FavoriteComics.Any();

        public FavoritesPageViewModel(IComicService comicService)
        {
            WeakReferenceMessenger.Default.Register<SearchMessage>(this);

            _comicService = comicService;
            _ = UpdateDisplayedComics();
        }

        public async void Receive(SearchMessage message) => await UpdateDisplayedComics(message.SearchText);

        private async Task UpdateDisplayedComics(string? searchText = null)
        {
            FavoriteComics.Clear();

            IsContentLoading = true;
            var comics = await _comicService.GetFavoriteComicsAsync(searchText, "all");

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
