using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Messages;
using Yukari.Services;

namespace Yukari.ViewModels
{
    internal partial class FavoritesPageViewModel : ObservableObject, IRecipient<SearchMessage>
    {
        private IComicService _comicService;

        [ObservableProperty] private ObservableCollection<ComicItemViewModel> _favoriteComics = new();

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
            var comics = await _comicService.GetFavoriteComicsAsync(searchText);

            FavoriteComics = new ObservableCollection<ComicItemViewModel>(
                comics.Select(comic => new ComicItemViewModel(comic, _comicService))
            );
        }

        [RelayCommand]
        private void NavigateToComic(string comicId)
        {
            WeakReferenceMessenger.Default.Send(new NavigateMessage(typeof(Views.ComicPage), comicId));
        }
    }
}
