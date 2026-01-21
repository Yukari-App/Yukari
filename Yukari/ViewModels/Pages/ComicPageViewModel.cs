using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class ComicPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;
        private readonly INotificationService _notificationService;

        private ContentKey? _comicKey;

        [ObservableProperty] public partial ComicModel? Comic { get; set; }
        [ObservableProperty] public partial List<ChapterItemViewModel>? Chapters { get; set; }

        [ObservableProperty]
        public partial string? SelectedLang { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIcon), nameof(IsDownloadAvailable))]
        public partial bool IsFavorite { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsInterfaceReady), nameof(IsContinueEnabled),
            nameof(IsDownloadAvailable), nameof(IsChapterOptionsAvailable),
            nameof(IsLanguageSelectionAvailable), nameof(IsChaptersEnabled))]
        public partial bool IsFavoriteStatusChanging { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsInterfaceReady), nameof(IsContinueEnabled),
            nameof(IsDownloadAvailable), nameof(IsChapterOptionsAvailable),
            nameof(IsLanguageSelectionAvailable), nameof(IsChaptersEnabled))]
        [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand), nameof(UpdateCommand))]
        public partial bool IsComicLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsInterfaceReady), nameof(NoChapters), nameof(IsContinueEnabled),
            nameof(IsDownloadAvailable), nameof(IsChapterOptionsAvailable),
            nameof(IsLanguageSelectionAvailable), nameof(IsChaptersEnabled))]
        [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand), nameof(UpdateCommand))]
        public partial bool IsChaptersLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        public partial bool IsAllChaptersDownloaded { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        public partial bool IsDownloadingAllChapters { get; set; }

        public bool NoChapters => !IsChaptersLoading && (Chapters == null || Chapters.Count == 0);
        public bool IsInterfaceReady => !IsFavoriteStatusChanging && !IsComicLoading && !IsChaptersLoading && !NoChapters;

        public bool IsContinueEnabled => IsInterfaceReady;
        public bool IsChapterOptionsAvailable => IsInterfaceReady;
        public bool IsChaptersEnabled => IsInterfaceReady;

        public bool IsLanguageSelectionAvailable => !IsFavoriteStatusChanging && !IsComicLoading && !IsChaptersLoading;
        public bool IsDownloadAvailable => IsFavorite && IsInterfaceReady;

        public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";
        public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : IsDownloadingAllChapters ? "\uF78A" : "\uE896";
        public string DownloadAllText => IsAllChaptersDownloaded ? "Delete All" : IsDownloadingAllChapters ? "Downloading..." : "Download All";

        public ComicPageViewModel(IComicService comicService, INotificationService notificationService)
        {
            _comicService = comicService;
            _notificationService = notificationService;
        }

        public async Task InitializeAsync(ContentKey ComicKey)
        {
            _comicKey = ComicKey;

            await RefreshComicAsync();
            await RefreshChaptersAsync();
        }

        private bool CanToggleFavorite() => Comic != null && !IsChaptersLoading;

        [RelayCommand(CanExecute = nameof(CanToggleFavorite))]
        public async Task ToggleFavoriteAsync()
        {
            if (_comicKey == null) return;
            IsFavoriteStatusChanging = true;

            bool newState = IsFavorite;

            Result result;
            if (newState)
            {
                result = await _comicService.UpsertFavoriteComicAsync(_comicKey);
                if (result.IsSuccess)
                    await _comicService.UpsertChaptersAsync(_comicKey, SelectedLang ?? "");
            }
            else result = await _comicService.RemoveFavoriteComicAsync(_comicKey);

            if (result.IsSuccess) await RefreshChaptersAsync();
            else
            {
                IsFavorite = !newState;
                _notificationService.ShowError(result.Error!);
            }

            IsFavoriteStatusChanging = false;
        }

        private bool CanUpdate() => Comic != null && !IsChaptersLoading;

        [RelayCommand(CanExecute = nameof(CanUpdate))]
        public async Task UpdateAsync()
        {
            if (_comicKey == null) return;

            var result = await _comicService.UpsertFavoriteComicAsync(_comicKey);
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!);
                return;
            }

            await _comicService.UpsertChaptersAsync(_comicKey, SelectedLang ?? "");

            await RefreshComicAsync();
            await RefreshChaptersAsync();

            _notificationService.ShowSuccess("Comic data updated successfully.");
        }

        private bool CanOpenInBrowser() => !string.IsNullOrEmpty(Comic?.ComicUrl);

        [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
        public async Task OpenInBrowserAsync() =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri(Comic!.ComicUrl!));

        private async Task RefreshComicAsync()
        {
            if (_comicKey == null) return;
            IsComicLoading = true;

            Comic = new ComicModel
            { 
                Id = "",
                Source = "",
                Title = "Loading...",
                Tags = new[] { "Loading..." },
                Year = 0,
            };

            var result = await _comicService.GetComicDetailsAsync(_comicKey);

            if (result.IsSuccess)
            {
                var comicAggregate = result.Value;
                if (comicAggregate == null)
                {
                    SetErrorStateForComics();
                    return;
                }

                Comic = comicAggregate.Comic;

                var userData = comicAggregate.UserData;
                IsFavorite = userData.IsFavorite;
                SelectedLang = userData.LastSelectedLang ?? Comic.Langs.FirstOrDefault()?.Key;
            }
            else
            {
                SetErrorStateForComics();
                _notificationService.ShowError(result.Error!);
            }

            IsComicLoading = false;
        }

        private void SetErrorStateForComics()
        {
            Comic = new ComicModel
            {
                Id = "",
                Source = "",
                Title = "Error",
                Author = "Error",
                Description = "An error occurred while loading the comic.",
                Tags = new[] { "N/A" },
                Year = 0,
                CoverImageUrl = null
            };
        }

        private async Task RefreshChaptersAsync()
        {
            if (IsComicLoading || _comicKey == null) return;
            IsChaptersLoading = true;

            if (string.IsNullOrEmpty(SelectedLang)) return;

            var result = await _comicService.GetAllChaptersAsync(_comicKey, SelectedLang);

            if (result.IsSuccess)
            {
                var chapterAggregates = result.Value;

                Chapters = chapterAggregates?
                    .Select(c => new ChapterItemViewModel(c, IsFavorite))
                    .ToList();
            }
            else
            {
                Chapters = null;
                _notificationService.ShowError(result.Error!);
            }

            IsChaptersLoading = false;
        }

        async partial void OnSelectedLangChanged(string? value)
        {
            if (IsComicLoading || value == null) return;
            
            await RefreshChaptersAsync();
            var result = await _comicService.UpsertComicUserDataAsync(_comicKey!, new()
            {
                IsFavorite = IsFavorite,
                LastSelectedLang = value
            });

            if (!result.IsSuccess) _notificationService.ShowError(result.Error!);
        }
    }
}
