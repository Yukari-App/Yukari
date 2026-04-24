using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class ComicPageViewModel
        : ObservableObject,
            IRecipient<ChapterUserDataUpdatedMessage>
    {
        private readonly IComicService _comicService;
        private readonly INotificationService _notificationService;
        private readonly IMessenger _messenger;

        private ContentKey? _comicKey;

        private CancellationTokenSource _navigationCts = new();
        private CancellationTokenSource _chaptersCts = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsComicAvailable))]
        public partial ComicModel? Comic { get; set; }

        [ObservableProperty]
        public partial List<ChapterItemViewModel>? Chapters { get; set; }

        [ObservableProperty]
        public partial string? SelectedLang { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIcon))]
        [NotifyCanExecuteChangedFor(nameof(ToggleDownloadAllChaptersCommand))]
        public partial bool IsFavorite { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInterfaceReady), nameof(IsLanguageSelectionAvailable))]
        [NotifyCanExecuteChangedFor(
            nameof(ContinueReadingCommand),
            nameof(ToggleDownloadAllChaptersCommand)
        )]
        public partial bool IsFavoriteStatusChanging { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInterfaceReady), nameof(IsLanguageSelectionAvailable))]
        [NotifyCanExecuteChangedFor(
            nameof(ContinueReadingCommand),
            nameof(ToggleFavoriteCommand),
            nameof(UpdateCommand),
            nameof(ToggleDownloadAllChaptersCommand)
        )]
        public partial bool IsComicLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsInterfaceReady),
            nameof(NoChapters),
            nameof(IsLanguageSelectionAvailable)
        )]
        [NotifyCanExecuteChangedFor(
            nameof(ContinueReadingCommand),
            nameof(ToggleFavoriteCommand),
            nameof(UpdateCommand),
            nameof(ToggleDownloadAllChaptersCommand)
        )]
        public partial bool IsChaptersLoading { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadAllIcon), nameof(DownloadAllText))]
        public partial bool IsAllChaptersDownloaded { get; set; }

        public bool IsComicAvailable => Comic?.IsAvailable ?? true;
        public bool NoChapters => !IsChaptersLoading && (Chapters == null || Chapters.Count == 0);
        public bool IsInterfaceReady =>
            !IsFavoriteStatusChanging && !IsComicLoading && !IsChaptersLoading && !NoChapters;

        public bool IsLanguageSelectionAvailable =>
            !IsFavoriteStatusChanging
            && !IsComicLoading
            && !IsChaptersLoading
            && Comic?.Langs.Length > 0;

        public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";
        public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : "\uE896";
        public string DownloadAllText => IsAllChaptersDownloaded ? "Delete All" : "Download All";

        public ComicPageViewModel(
            IComicService comicService,
            INotificationService notificationService,
            IMessenger messenger
        )
        {
            _comicService = comicService;
            _notificationService = notificationService;
            _messenger = messenger;

            _messenger.RegisterAll(this);
        }

        public void Receive(ChapterUserDataUpdatedMessage message) =>
            Chapters?.FirstOrDefault(c => c.Key.Equals(message.ChapterKey))?.RefreshUserDataAsync();

        public async Task InitializeAsync(ContentKey comicKey)
        {
            _comicKey = comicKey;

            await RefreshComicAsync();
            await RefreshChaptersAsync();
        }

        public void OnNavigatedFrom()
        {
            _navigationCts.Cancel();
            _navigationCts.Dispose();
        }

        private bool CanContinueReading() => Comic != null && IsInterfaceReady;

        [RelayCommand(CanExecute = nameof(CanContinueReading))]
        private async Task ContinueReadingAsync()
        {
            if (_comicKey == null || Comic == null || SelectedLang == null)
                return;

            var result = await _comicService.GetComicReadingProgressAsync(_comicKey, SelectedLang);

            if (!result.IsSuccess)
            {
                HandleNotSuccessResult(result);
                return;
            }

            var currentProgress = result.Value;

            var chapterKey = !string.IsNullOrEmpty(currentProgress?.LastChapterId)
                ? new ContentKey(currentProgress.LastChapterId, Comic.Source)
                : null;

            _messenger.Send(
                new SwitchAppModeMessage(
                    AppMode.Reader,
                    new ReaderNavigationArgs(_comicKey, Comic.Title, chapterKey, SelectedLang, true)
                )
            );
        }

        private bool CanToggleFavorite() => Comic != null && !IsChaptersLoading;

        [RelayCommand(CanExecute = nameof(CanToggleFavorite))]
        private async Task ToggleFavoriteAsync()
        {
            if (_comicKey == null)
                return;
            IsFavoriteStatusChanging = true;

            bool newState = IsFavorite;

            Result result;
            if (newState)
            {
                result = await _comicService.UpsertFavoriteComicAsync(_comicKey);
                if (result.IsSuccess)
                    await _comicService.UpsertChaptersAsync(_comicKey, SelectedLang ?? "");
            }
            else
                result = await _comicService.RemoveFavoriteComicAsync(_comicKey);

            if (result.IsSuccess)
                await RefreshChaptersAsync();
            else
            {
                IsFavorite = !newState;
                HandleNotSuccessResult(result);
            }

            IsFavoriteStatusChanging = false;
        }

        private bool CanUpdate() => Comic != null && !IsChaptersLoading;

        [RelayCommand(CanExecute = nameof(CanUpdate))]
        private async Task UpdateAsync()
        {
            if (_comicKey == null)
                return;

            var result = await _comicService.UpsertFavoriteComicAsync(_comicKey);
            if (!result.IsSuccess)
            {
                HandleNotSuccessResult(result);
                return;
            }

            await _comicService.UpsertChaptersAsync(_comicKey, SelectedLang ?? "");

            await RefreshComicAsync();
            await RefreshChaptersAsync();

            _notificationService.ShowSuccess("Comic data updated successfully.");
        }

        private bool CanOpenInBrowser() => !string.IsNullOrEmpty(Comic?.ComicUrl);

        [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
        private async Task OpenInBrowserAsync() =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri(Comic!.ComicUrl!));

        private bool CanToggleDownloadAllChapters() => IsFavorite && IsInterfaceReady;

        [RelayCommand(CanExecute = nameof(CanToggleDownloadAllChapters))]
        private void ToggleDownloadAllChapters()
        {
            _notificationService.ShowWarning("Download chapters feature is not implemented yet.");
            IsAllChaptersDownloaded = !IsAllChaptersDownloaded;
        }

        [RelayCommand]
        private void NavigateToReader(ContentKey chapterKey) =>
            _messenger.Send(
                new SwitchAppModeMessage(
                    AppMode.Reader,
                    new ReaderNavigationArgs(_comicKey!, Comic!.Title, chapterKey, SelectedLang!)
                )
            );

        [RelayCommand]
        private async Task MarkPreviousChaptersAsRead(ChapterItemViewModel item)
        {
            var index = Chapters!.IndexOf(item);
            if (index <= 0)
                return;

            var chapterIDs = Chapters.Take(index).Select(c => c.Key.Id).ToArray();

            await UpdateChaptersReadStatusAsync(chapterIDs, true);
        }

        [RelayCommand]
        private Task MarkAllAsRead() => MarkAllChaptersReadStatus(true);

        [RelayCommand]
        private Task MarkAllAsUnread() => MarkAllChaptersReadStatus(false);

        private async Task RefreshComicAsync()
        {
            if (_comicKey == null)
                return;
            IsComicLoading = true;

            Comic = new ComicModel
            {
                Id = "",
                Source = "",
                Title = "Loading...",
                Tags = new[] { "Loading..." },
                Year = 0,
            };

            var result = await _comicService.GetComicDetailsAsync(
                _comicKey,
                ct: _navigationCts.Token
            );

            if (result.IsCancelled)
                return;

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
                HandleNotSuccessResult(result);
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
                CoverImageUrl = null,
            };
        }

        private async Task RefreshChaptersAsync()
        {
            if (IsComicLoading || _comicKey == null)
                return;

            _chaptersCts.Cancel();
            _chaptersCts.Dispose();
            _chaptersCts = new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _chaptersCts.Token,
                _navigationCts.Token
            );

            IsChaptersLoading = true;

            if (string.IsNullOrEmpty(SelectedLang))
            {
                Chapters = null;
                IsChaptersLoading = false;
                return;
            }

            var result = await _comicService.GetAllChaptersAsync(
                _comicKey,
                SelectedLang,
                ct: linkedCts.Token
            );

            if (result.IsCancelled)
                return;

            if (result.IsSuccess)
            {
                var chapterAggregates = result.Value;

                Chapters = chapterAggregates
                    ?.Select(c => new ChapterItemViewModel(
                        _comicService,
                        _notificationService,
                        c,
                        _comicKey,
                        IsFavorite,
                        NavigateToReaderCommand,
                        MarkPreviousChaptersAsReadCommand
                    ))
                    .ToList();
            }
            else
            {
                Chapters = null;
                HandleNotSuccessResult(result);
            }

            IsChaptersLoading = false;
        }

        private async Task MarkAllChaptersReadStatus(bool isRead)
        {
            if (Chapters == null || Chapters.Count == 0)
                return;

            var chapterIDs = Chapters.Select(c => c.Key.Id).ToArray();
            await UpdateChaptersReadStatusAsync(chapterIDs, isRead);
        }

        private async Task UpdateChaptersReadStatusAsync(string[] chapterIDs, bool isRead)
        {
            var result = await _comicService.UpsertChaptersIsReadAsync(
                _comicKey!,
                chapterIDs,
                isRead
            );
            if (!result.IsSuccess)
            {
                HandleNotSuccessResult(result);
                return;
            }

            await RefreshChaptersAsync();
        }

        private void HandleNotSuccessResult(Result result)
        {
            if (result.Kind == ResultKind.ComicSourceDisabled)
                _notificationService.ShowWarning(result.Error!, "Source Disabled");
            else if (!result.IsSuccess)
                _notificationService.ShowError(result.Error!, result.ErrorTitle!);
        }

        async partial void OnSelectedLangChanged(string? value)
        {
            if (IsComicLoading)
                return;

            await RefreshChaptersAsync();

            if (string.IsNullOrEmpty(value))
                return;

            var result = await _comicService.UpsertComicUserDataAsync(
                _comicKey!,
                new() { IsFavorite = IsFavorite, LastSelectedLang = value }
            );

            if (!result.IsSuccess)
                HandleNotSuccessResult(result);
        }
    }
}
