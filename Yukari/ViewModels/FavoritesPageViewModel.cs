using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Composition.Interactions;
using System.Collections.ObjectModel;
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
    }
}
