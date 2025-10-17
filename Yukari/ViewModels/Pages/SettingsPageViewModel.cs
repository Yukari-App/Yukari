using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Services.Comics;

namespace Yukari.ViewModels.Pages
{
    internal partial class SettingsPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;

        [ObservableProperty]
        private ComicSourceModel _defaultComicSource;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsComicSourcesEmpty))]
        private ObservableCollection<ComicSourceModel> _comicSources = new();

        public bool IsComicSourcesEmpty => ComicSources.Count == 0;

        public SettingsPageViewModel(IComicService comicService)
        {
            _comicService = comicService;

            _ = LoadComicSourcesAsync();
        }

        private async Task LoadComicSourcesAsync()
        {
            ComicSources = new(await _comicService.GetComicSourcesAsync());
        }
    }
}
