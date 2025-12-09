using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class ComicPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;

        private ContentKey? _comicKey;
        private ComicModel? _comic;
        private ComicUserData _comicUserData;

        [ObservableProperty] private string _title = "Loading...";
        [ObservableProperty] private string _author = "Loading Author...";
        [ObservableProperty] private string _description = "Loading Description..";
        [ObservableProperty] private string[] _tags = new[] { "Loading Tags..." };
        [ObservableProperty] private int _year = 0;
        [ObservableProperty] private string? _coverImageUrl;

        [ObservableProperty] private List<LanguageModel> _langs = new();
        [ObservableProperty] private List<ChapterItemViewModel> _chapters = new();

        [ObservableProperty]
        private string? _selectedLang;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIcon))]
        private bool _isFavorite;

        [ObservableProperty,NotifyPropertyChangedFor(
            nameof(NoChapters), nameof(IsChapterOptionsAvailable), nameof(IsLanguageSelectionAvailable), nameof(IsContinueEnabled))]
        private bool _isChaptersLoading = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        private bool _isAllChaptersDownloaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        private bool _isDownloadingAllChapters;

        public bool NoChapters => !IsChaptersLoading && Chapters.Count == 0;

        public bool IsContinueEnabled => !IsChaptersLoading && !NoChapters;
        public bool IsChapterOptionsAvailable => !IsChaptersLoading && Chapters.Count > 0;
        public bool IsLanguageSelectionAvailable => !IsChaptersLoading && Langs.Count > 0;

        public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";
        public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : IsDownloadingAllChapters ? "\uF78A" : "\uE896";
        public string DownloadAllText => IsAllChaptersDownloaded ? "Delete All" : IsDownloadingAllChapters ? "Downloading..." : "Download All";

        public ComicPageViewModel(IComicService comicService) =>
            _comicService = comicService;

        public async Task InitializeAsync(ContentKey ComicKey)
        {
            _comicKey = ComicKey;

            var comicAggregate = await _comicService.GetComicDetailsAsync(_comicKey);

            Title = _comic?.Title ?? "Unknown Title";
            Author = _comic?.Author ?? "Unknown Author";
            Description = _comic?.Description ?? "No description available.";
            Tags = _comic?.Tags ?? new[] { "N/A" };
            Year = _comic?.Year ?? 0;
            CoverImageUrl = _comic?.CoverImageUrl;
            Langs = await LoadLangs();

            IsFavorite = _comicUserData?.IsFavorite ?? false;
            SelectedLang = _comicUserData?.LastSelectedLang ?? Langs.FirstOrDefault()?.Code;

            await UpdateDisplayedChaptersAsync();
        }

        [RelayCommand]
        public void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
        }

        private async Task UpdateDisplayedChaptersAsync()
        {
            if (string.IsNullOrEmpty(SelectedLang))
            {
                if (IsChaptersLoading) IsChaptersLoading = false;
                return;
            }

            IsChaptersLoading = true;

            Chapters = (await _comicService.GetAllChaptersAsync(_comicKey!, SelectedLang))
                .Select(chapter => new ChapterItemViewModel(chapter)).ToList();

            IsChaptersLoading = false;
        }

        private async Task<List<LanguageModel>> LoadLangs()
        {
            var sourceLangs = await _comicService.GetSourceLanguagesAsync(_comicKey!.Source);

            return _comic?.Langs?.Select(code => new LanguageModel(
                    code,
                    sourceLangs.TryGetValue(code, out var displayName) ? displayName : code
                )).ToList() ?? new List<LanguageModel>();
        }

        async partial void OnSelectedLangChanged(string? value) =>
            await UpdateDisplayedChaptersAsync();
    }
}
