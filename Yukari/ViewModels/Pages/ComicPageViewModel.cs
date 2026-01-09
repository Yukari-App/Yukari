using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Models;
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
        private bool _isInitializing;

        [ObservableProperty] public partial string Title { get; set; } = "Loading...";
        [ObservableProperty] public partial string Author { get; set; } = "Loading Author...";
        [ObservableProperty] public partial string Description { get; set; } = "Loading Description...";
        [ObservableProperty] public partial string[] Tags { get; set; } = new[] { "Loading Tags..." };
        [ObservableProperty] public partial int? Year { get; set; } = 0;
        [ObservableProperty] public partial string? CoverImageUrl { get; set; }

        [ObservableProperty] public partial List<LanguageModel>? Langs { get; set; }
        [ObservableProperty] public partial List<ChapterItemViewModel>? Chapters { get; set; }

        [ObservableProperty]
        public partial string? SelectedLang { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIcon), nameof(IsDownloadAvailable))]
        public partial bool IsFavorite { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(NoChapters),
            nameof(IsContinueEnabled),
            nameof(IsDownloadAvailable),
            nameof(IsChapterOptionsAvailable),
            nameof(IsLanguageSelectionAvailable))]
        public partial bool IsChaptersLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        public partial bool IsAllChaptersDownloaded { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        public partial bool IsDownloadingAllChapters { get; set; }

        public bool NoChapters => !IsChaptersLoading && Chapters?.Count == 0;

        public bool IsContinueEnabled => !IsChaptersLoading && !NoChapters;
        public bool IsDownloadAvailable => IsFavorite && !NoChapters;
        public bool IsChapterOptionsAvailable => !IsChaptersLoading && Chapters?.Count > 0;
        public bool IsLanguageSelectionAvailable => !IsChaptersLoading && Langs?.Count > 0;

        public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";
        public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : IsDownloadingAllChapters ? "\uF78A" : "\uE896";
        public string DownloadAllText => IsAllChaptersDownloaded ? "Delete All" : IsDownloadingAllChapters ? "Downloading..." : "Download All";

        public ComicPageViewModel(IComicService comicService) =>
            _comicService = comicService;

        public async Task InitializeAsync(ContentKey ComicKey)
        {
            _isInitializing = true;
            _comicKey = ComicKey;

            var comicAggregate = await _comicService.GetComicDetailsAsync(_comicKey);
            if (comicAggregate == null)
            {
                Title = "Comic Not Found";
                Author = "";
                Description = "The requested comic could not be found.";
                Tags = new[] { "N/A" };
                Year = 0;
                CoverImageUrl = null;
                Langs = new List<LanguageModel>();
                Chapters = new List<ChapterItemViewModel>();
                IsChaptersLoading = false;
                _isInitializing = false;
                return;
            }

            _comic = comicAggregate.Comic;
            var userData = comicAggregate.UserData;

            Title = _comic.Title;
            Author = _comic.Author ?? "Unknown Author";
            Description = _comic.Description ?? "No description available.";
            Tags = _comic.Tags;
            Year = _comic.Year;
            CoverImageUrl = _comic.CoverImageUrl;
            Langs = _comic.Langs.ToList();

            IsFavorite = userData.IsFavorite;
            SelectedLang = userData.LastSelectedLang ?? Langs.FirstOrDefault()?.Key;

            _isInitializing = false;
            await UpdateDisplayedChaptersAsync();
        }

        [RelayCommand]
        public void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
        }

        private async Task UpdateDisplayedChaptersAsync()
        {
            if (_isInitializing || _comicKey == null || string.IsNullOrEmpty(SelectedLang))
                return;

            IsChaptersLoading = true;

            try
            {
                var chapterAggregates = await _comicService.GetAllChaptersAsync(_comicKey, SelectedLang);

                Chapters = chapterAggregates
                    .Select(c => new ChapterItemViewModel(c, IsFavorite))
                    .ToList();
            }
            finally
            {
                IsChaptersLoading = false;
            }
        }

        async partial void OnSelectedLangChanged(string? value)
        {
            if (!_isInitializing)
                await UpdateDisplayedChaptersAsync();
        }
    }
}
