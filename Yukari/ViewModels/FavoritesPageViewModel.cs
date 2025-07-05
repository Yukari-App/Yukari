using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class FavoritesPageViewModel : ObservableObject
    {
        private IMangaService _mangaService;

        [ObservableProperty] private ObservableCollection<Manga> _favoriteMangas = new();

        public FavoritesPageViewModel(IMangaService mangaService)
        {
            _mangaService = mangaService;
        }

        public async Task LoadFavoriteMangasAsync()
        {
            FavoriteMangas.Clear();

            try
            {
                var mangas = await _mangaService.GetFavoriteMangasAsync();

                foreach (var manga in mangas)
                {
                    FavoriteMangas.Add(manga);
                }
            }
            finally
            {
                
            }
        }

        [RelayCommand]
        private void NavigateToManga(Guid mangaId)
        {
            WeakReferenceMessenger.Default.Send(new NavigateMessage(typeof(Views.MangaPage), mangaId));
        }
    }
}
