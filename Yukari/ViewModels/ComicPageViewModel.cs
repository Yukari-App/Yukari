using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Services;

namespace Yukari.ViewModels
{
    internal partial class ComicPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;

        private string _comicId;
        private ComicModel _comic;

        public string Title => _comic?.Title ?? "Loading...";
        public string Author => _comic?.Author ?? "Unknown Author";
        public string Description => _comic?.Description ?? "No description available.";
        public string[] Tags => _comic?.Tags ?? ["N/A"];
        public int Year => _comic?.Year ?? 0;
        public string? CoverImageUrl => _comic?.CoverImageUrl;
        public string[] Langs => _comic?.Langs ?? ["N/A"];

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

        public ComicPageViewModel(IComicService comicService)
        {
            _comicService = comicService;
        }

        public async Task InitializeAsync(string comicId)
        {
            _comicId = comicId;

            _comic = await _comicService.GetComicByIdAsync(comicId);

            IsFavorite = _comic?.IsFavorite ?? false;
            SelectedLang = _comic?.LastSelectedLang ?? Langs[0];

            var chapters = await _comicService.GetAllChaptersAsync(comicId);

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
