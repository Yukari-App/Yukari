using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Yukari.Models;

namespace Yukari.ViewModels
{
    public partial class FavoritesPageViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Manga> _favoriteMangas = new();

        public FavoritesPageViewModel()
        {

        }
    }
}
