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
    public partial class DiscoverPageViewModel : ObservableObject, IRecipient<SearchMessage>
    {
        private IComicService _comicService;

        [ObservableProperty] private ObservableCollection<ComicItemViewModel> _searchedComics = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NoResults))]
        private bool _isContentLoading = true;

        public bool NoResults => !IsContentLoading && !SearchedComics.Any();

        public DiscoverPageViewModel(IComicService comicService)
        {
            WeakReferenceMessenger.Default.Register<SearchMessage>(this);

            _comicService = comicService;
        }

        public async void Receive(SearchMessage message) => await UpdateDisplayedComicsAsync(message.SearchText);

        public async Task UpdateDisplayedComicsAsync(string? searchText = null)
        {
            SearchedComics.Clear();

            IsContentLoading = true;
            var comics = await _comicService.SearchComicsAsync("MangaDex", searchText, []);

            SearchedComics = new ObservableCollection<ComicItemViewModel>(
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
