using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Services.Comics;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class ComicPageViewModel : ObservableObject
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
        [ObservableProperty] private List<LanguageModel> _langs;

        [ObservableProperty] private ObservableCollection<ChapterItemViewModel> _chapters = new();

        [ObservableProperty]
        private string? _selectedLang;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIcon))]
        private bool _isFavorite;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(NoChapters), nameof(IsDownloadAllEnabled))]
        private bool _isChaptersLoading = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        private bool _isAllChaptersDownloaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        private bool _isDownloadingAllChapters;

        public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";

        public bool NoChapters => !IsChaptersLoading && !Chapters.Any();

        public bool IsDownloadAllEnabled => !IsChaptersLoading && Chapters.Any();

        public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : IsDownloadingAllChapters ? "\uF78A" : "\uE896";
        public string DownloadAllText => IsAllChaptersDownloaded ? "Delete All" : IsDownloadingAllChapters ? "Downloading..." : "Download All";

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
            Langs = await LoadLangs();

            IsFavorite = _comic?.IsFavorite ?? false;
            SelectedLang = _comic?.LastSelectedLang ?? Langs.FirstOrDefault()?.Code;

            await UpdateDisplayedChaptersAsync();
        }

        [RelayCommand]
        public void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
        }

        private async Task UpdateDisplayedChaptersAsync()
        {
            if (_comicIdentifier == null || string.IsNullOrEmpty(SelectedLang))
                return;

            Chapters.Clear();

            IsChaptersLoading = true;
            var chapters = await _comicService.GetAllChaptersAsync(_comicIdentifier, SelectedLang);

            Chapters = new ObservableCollection<ChapterItemViewModel>(
                chapters.Select(chapter => new ChapterItemViewModel(chapter))
            );

            IsChaptersLoading = false;
        }

        private async Task<List<LanguageModel>> LoadLangs()
        {
            var sourceLangs = await _comicService.GetSourceLanguagesAsync(_comicIdentifier.Source);

            return _comic.Langs?.Select(code => new LanguageModel(
                    code,
                    sourceLangs.TryGetValue(code, out var displayName) ? displayName : code
                )).ToList() ?? new List<LanguageModel>();
        }

        async partial void OnSelectedLangChanged(string value) =>
            await UpdateDisplayedChaptersAsync();
    }
}
