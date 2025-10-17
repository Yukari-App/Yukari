using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Services;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
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
        }

        public async void Receive(SearchMessage message) => await UpdateDisplayedComicsAsync(message.SearchText);

        public async Task InitializeAsync() => await UpdateDisplayedComicsAsync();

        private async Task UpdateDisplayedComicsAsync(string? searchText = null)
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
            WeakReferenceMessenger.Default.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), comicIdentifier));
        }
    }
}
