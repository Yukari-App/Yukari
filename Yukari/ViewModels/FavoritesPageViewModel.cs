using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Messages;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class FavoritesPageViewModel : ObservableObject, IRecipient<SearchMessage>
    {
        private IMangaService _mangaService;

        [ObservableProperty] private ObservableCollection<MangaItemViewModel> _favoriteMangas = new();

        public FavoritesPageViewModel(IMangaService mangaService)
        {
            WeakReferenceMessenger.Default.Register<SearchMessage>(this);

            _mangaService = mangaService;
            _ = UpdateDisplayedMangas();
        }

        public void Receive(SearchMessage message)
        {
            _ = UpdateDisplayedMangas(message.SearchText);
        }

        private async Task UpdateDisplayedMangas(string? searchText = null)
        {
            var mangas = await _mangaService.GetFavoriteMangasAsync(searchText);

            FavoriteMangas = new ObservableCollection<MangaItemViewModel>(
                mangas.Select(manga => new MangaItemViewModel(manga, _mangaService))
            );
        }

        [RelayCommand]
        private void NavigateToManga(Guid mangaId)
        {
            WeakReferenceMessenger.Default.Send(new NavigateMessage(typeof(Views.MangaPage), mangaId));
        }
    }
}
