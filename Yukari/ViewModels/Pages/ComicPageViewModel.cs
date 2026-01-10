using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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

        [ObservableProperty] public partial string? Title { get; set; }
        [ObservableProperty] public partial string? Author { get; set; }
        [ObservableProperty] public partial string? Description { get; set; }
        [ObservableProperty] public partial string[]? Tags { get; set; }
        [ObservableProperty] public partial int? Year { get; set; }
        [ObservableProperty] public partial string? CoverImageUrl { get; set; }

        [ObservableProperty] public partial List<LanguageModel>? Langs { get; set; }
        [ObservableProperty] public partial List<ChapterItemViewModel>? Chapters { get; set; }

        [ObservableProperty]
        public partial string? SelectedLang { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIcon), nameof(IsDownloadAvailable))]
        public partial bool IsFavorite { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsContinueEnabled))]
        [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand))]
        public partial bool IsComicLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(NoChapters),
            nameof(IsContinueEnabled),
            nameof(IsDownloadAvailable),
            nameof(IsChapterOptionsAvailable),
            nameof(IsLanguageSelectionAvailable))]
        [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand))]
        public partial bool IsChaptersLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        public partial bool IsAllChaptersDownloaded { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        public partial bool IsDownloadingAllChapters { get; set; }

        public bool NoChapters => !IsChaptersLoading && (Chapters is not { Count: > 0 });

        public bool IsContinueEnabled => !IsChaptersLoading && !NoChapters;
        public bool IsDownloadAvailable => IsFavorite && !NoChapters;
        public bool IsChapterOptionsAvailable => !NoChapters;
        public bool IsLanguageSelectionAvailable => !IsChaptersLoading && Langs?.Count > 0;

        public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";
        public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : IsDownloadingAllChapters ? "\uF78A" : "\uE896";
        public string DownloadAllText => IsAllChaptersDownloaded ? "Delete All" : IsDownloadingAllChapters ? "Downloading..." : "Download All";

        public ComicPageViewModel(IComicService comicService) =>
            _comicService = comicService;

        public async Task InitializeAsync(ContentKey ComicKey)
        {
            _comicKey = ComicKey;

            await RefreshComicAsync();
            await RefreshChaptersAsync();
        }

        private bool CanToggleFavorite() => _comic != null && !IsChaptersLoading;

        [RelayCommand(CanExecute = nameof(CanToggleFavorite))]
        public async Task ToggleFavoriteAsync()
        {
            if (_comic == null || _comicKey == null) return;

            var previousState = IsFavorite;
            IsFavorite = !IsFavorite;

            try
            {
                bool success;
                if (IsFavorite) success = await _comicService.UpsertFavoriteComicAsync(_comic, SelectedLang ?? "");
                else success = await _comicService.RemoveFavoriteComicAsync(_comicKey);

                if (success) await RefreshChaptersAsync();
                else
                {
                    IsFavorite = previousState;
                    // TO-DO: Trigger a visual error notification here
                }
            }
            catch (Exception)
            {
                IsFavorite = previousState;
                // TO-DO: Trigger a visual error notification here
            }
        }

        private bool CanOpenInBrowser() => !string.IsNullOrEmpty(_comic?.ComicUrl);

        [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
        public async Task OpenInBrowserAsync() =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri(_comic!.ComicUrl!));

        private async Task RefreshComicAsync()
        {
            if (_comicKey == null) return;
            IsComicLoading = true;

            Title = Author = Description = "Loading...";
            Tags = new[] { "Loading..." };
            Year = 0;

            try
            {
                var comicAggregate = await _comicService.GetComicDetailsAsync(_comicKey);
                if (comicAggregate == null)
        {
                    SetErrorStateForComics();
                return;
                }

                _comic = comicAggregate.Comic;
                var userData = comicAggregate.UserData;
                Title = _comic.Title;
                Author = _comic.Author ?? "Unknown Author";
                Description = _comic.Description ?? "No description available.";
                Tags = _comic.Tags;
                Year = _comic.Year ?? 0;
                CoverImageUrl = _comic.CoverImageUrl;
                Langs = _comic.Langs.ToList();

                IsFavorite = userData.IsFavorite;
                SelectedLang = userData.LastSelectedLang ?? Langs.FirstOrDefault()?.Key;
            }
            catch
            {
                SetErrorStateForComics();
                // TO-DO: Trigger a visual error notification here
            }
            finally
            {
                IsComicLoading = false;
            }
        }

        private void SetErrorStateForComics()
        {
            Title = "Error Loading Comic";
            Author = "Error";
            Description = "An error occurred while loading the comic.";
            Tags = new[] { "N/A" };
            Year = 0;
            CoverImageUrl = null;
        }

        private async Task RefreshChaptersAsync()
        {
            if (IsComicLoading || _comicKey == null) return;
            IsChaptersLoading = true;

            try
            {
                if (string.IsNullOrEmpty(SelectedLang)) return;

                var chapterAggregates = await _comicService.GetAllChaptersAsync(_comicKey, SelectedLang);

                Chapters = chapterAggregates
                    .Select(c => new ChapterItemViewModel(c, IsFavorite))
                    .ToList();
            }
            catch (Exception)
            {
                // TO-DO: Trigger a visual error notification here
            }
            finally
            {
                IsChaptersLoading = false;
            }
        }

        async partial void OnSelectedLangChanged(string? value)
        {
            if (!IsComicLoading)
                await RefreshChaptersAsync();
        }
    }
}
