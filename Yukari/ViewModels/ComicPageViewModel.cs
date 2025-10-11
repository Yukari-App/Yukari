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

        private ContentIdentifier _comicIdentifier;
        private ComicModel _comic;

        [ObservableProperty] private string _title = "Loading...";
        [ObservableProperty] private string _author = "Unknown Author";
        [ObservableProperty] private string _description = "No description available.";
        [ObservableProperty] private string[] _tags = new[] { "N/A" };
        [ObservableProperty] private int _year;
        [ObservableProperty] private string? _coverImageUrl;
        [ObservableProperty] private string[] _langs = new[] { "N/A" };

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

        public async Task InitializeAsync(ContentIdentifier comicIdentifier)
        {
            _comicIdentifier = comicIdentifier;

            _comic = await _comicService.GetComicDetailsAsync(comicIdentifier);

            Title = _comic?.Title ?? "Loading...";
            Author = _comic?.Author ?? "Unknown Author";
            Description = _comic?.Description ?? "No description available.";
            Tags = _comic?.Tags ?? new[] { "N/A" };
            Year = _comic?.Year ?? 0;
            CoverImageUrl = _comic?.CoverImageUrl;
            Langs = _comic?.Langs ?? new[] { "N/A" };

            IsFavorite = _comic?.IsFavorite ?? false;
            SelectedLang = _comic?.LastSelectedLang ?? Langs[0];

            var chapters = await _comicService.GetAllChaptersAsync(comicIdentifier, _comic.LastSelectedLang ?? Langs[0]);
            SelectedLang = _comic?.LastSelectedLang ?? Langs[0];

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
