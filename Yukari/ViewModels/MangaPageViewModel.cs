using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class MangaPageViewModel : ObservableObject
    {
        private readonly IMangaService _mangaService;

        private Guid _mangaId;
        private Manga _manga;

        public string Title => _manga?.Title ?? "Loading...";
        public string Author => _manga?.Author ?? "Unknown Author";
        public string Description => _manga?.Description ?? "No description available.";
        public string[] Tags => _manga?.Tags ?? ["N/A"];
        public int Year => _manga?.Year ?? 0;
        public string CoverImageUrl => _manga?.CoverImageUrl;
        public string[] Langs => _manga?.Langs ?? ["N/A"];

        [ObservableProperty]
        private string _selectedLang;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIcon))]
        private bool _isFavorite;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        private bool _isAllChaptersDownloaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        private bool _isDownloadingAllChapters;

        public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";

        public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : IsDownloadingAllChapters ? "\uF78A" : "\uE896";
        public string DownloadAllText => IsAllChaptersDownloaded ? "Delete All" : IsDownloadingAllChapters ? "Downloading..." : "Download All";

        [ObservableProperty] private ObservableCollection<ChapterItemViewModel> _chapters = new();

        public MangaPageViewModel(IMangaService mangaService)
        {
            _mangaService = mangaService;
        }

        public async Task InitializeAsync(Guid mangaId)
        {
            _mangaId = mangaId;

            _manga = await _mangaService.GetMangaByIdAsync(mangaId);

            IsFavorite = _manga?.IsFavorite ?? false;
            SelectedLang = _manga?.LastSelectedLang ?? Langs[0];

            var chapters = await _mangaService.GetAllMangaChaptersAsync(mangaId);

            Chapters = new ObservableCollection<ChapterItemViewModel>(
                chapters.Select(chapter => new ChapterItemViewModel(chapter))
            );
        }

        [RelayCommand]
        public void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
        }
    }
}
