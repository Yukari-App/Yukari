using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly IMangaService _mangaService;

        [ObservableProperty]
        private MangaSourceModel _defaultMangaSource;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMangaSourcesEmpty))]
        private ObservableCollection<MangaSourceModel> _mangaSources = new();

        public bool IsMangaSourcesEmpty => MangaSources.Count == 0;

        public SettingsPageViewModel(IMangaService mangaService)
        {
            _mangaService = mangaService;

            _ = LoadMangaSourcesAsync();
        }

        private async Task LoadMangaSourcesAsync()
        {
            MangaSources = new(await _mangaService.GetMangaSourcesAsync());
        }
    }
}
