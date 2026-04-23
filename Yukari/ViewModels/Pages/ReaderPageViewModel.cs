using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Helpers.UI;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class ReaderPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;
        private readonly ISettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly IMessenger _messenger;

        private readonly ReaderDisplayContext _displayContext = new();

        private ContentKey? _comicKey;
        private string? _language;
        private ChapterAggregate[]? _chapters;

        private int _currentChapterIndex = -1;

        private CancellationTokenSource _navigationCts = new();
        private CancellationTokenSource _chapterLoadCts = new();

        [ObservableProperty]
        public partial string? ComicTitle { get; set; }

        [ObservableProperty]
        public partial string? ChapterTitle { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(NextChapterCommand), nameof(PreviousChapterCommand))]
        public partial ChapterModel? CurrentChapter { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageIndicatorText))]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
        public partial List<ChapterPageItemViewModel>? ChapterPages { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageIndicatorText))]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
        public partial int CurrentPageIndex { get; set; } = 0;

        public string PageIndicatorText =>
            ChapterPages != null && ChapterPages.Count > 0
                ? $"{CurrentPageIndex + 1} / {ChapterPages.Count}"
                : "0 / 0";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullscreenButtonIcon))]
        public partial bool IsFullscreen { get; set; } = false;

        public string FullscreenButtonIcon => IsFullscreen ? "\uE92C" : "\uE92D";

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(ForwardChapterNavigationButtonCommand),
            nameof(BackwardChapterNavigationButtonCommand),
            nameof(ForwardChapterNavigationButtonToolTip),
            nameof(BackwardChapterNavigationButtonToolTip),
            nameof(IsHorizontalPageNavigationButtonsVisible),
            nameof(IsVerticalPageNavigationButtonsVisible),
            nameof(ForwardPageNavigationButtonCommand),
            nameof(BackwardPageNavigationButtonCommand)
        )]
        public partial ReadingMode ReadingMode { get; set; } = ReadingMode.RightToLeft;

        public IRelayCommand ForwardChapterNavigationButtonCommand =>
            ReadingMode == ReadingMode.RightToLeft ? NextChapterCommand : PreviousChapterCommand;

        public IRelayCommand BackwardChapterNavigationButtonCommand =>
            ReadingMode == ReadingMode.RightToLeft ? PreviousChapterCommand : NextChapterCommand;

        public string ForwardChapterNavigationButtonToolTip =>
            ReadingMode == ReadingMode.RightToLeft ? "Next Chapter" : "Previous Chapter";

        public string BackwardChapterNavigationButtonToolTip =>
            ReadingMode == ReadingMode.RightToLeft ? "Previous Chapter" : "Next Chapter";

        public bool IsHorizontalPageNavigationButtonsVisible =>
            ReadingMode is ReadingMode.RightToLeft or ReadingMode.LeftToRight;

        public bool IsVerticalPageNavigationButtonsVisible => ReadingMode == ReadingMode.Vertical;

        public IRelayCommand ForwardPageNavigationButtonCommand =>
            ReadingMode == ReadingMode.RightToLeft ? NextPageCommand : PreviousPageCommand;

        public IRelayCommand BackwardPageNavigationButtonCommand =>
            ReadingMode == ReadingMode.RightToLeft ? PreviousPageCommand : NextPageCommand;

        [ObservableProperty]
        public partial ScalingMode ScalingMode { get; set; } = ScalingMode.FitScreen;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(NextChapterCommand), nameof(PreviousChapterCommand))]
        public partial bool IsLoading { get; set; } = true;

        public ReaderPageViewModel(
            IComicService comicService,
            ISettingsService settingsService,
            INotificationService notificationService,
            IMessenger messenger
        )
        {
            _comicService = comicService;
            _settingsService = settingsService;
            _notificationService = notificationService;
            _messenger = messenger;

            LoadReaderSettings();
        }

        public async Task InitializeAsync(
            ContentKey comicKey,
            string comicTitle,
            ContentKey? chapterKey,
            string selectedLang,
            bool navigationFromContinueButton
        )
        {
            _comicKey = comicKey;
            _language = selectedLang;
            ComicTitle = comicTitle;

            var result = await _comicService.GetAllChaptersAsync(
                comicKey,
                selectedLang,
                ct: _navigationCts.Token
            );

            if (result.IsCancelled)
                return;

            if (!result.IsSuccess)
            {
                await HandleNotSuccessResultAndNavigateBack(result);
                return;
            }

            _chapters = result.Value!.ToArray();

            if (_chapters.Length == 0)
            {
                await TriggerNotificationAndNavigateBack(() =>
                    _notificationService.ShowWarning(
                        "No chapters found for this language.",
                        "No Chapters Found"
                    )
                );
                return;
            }

            _currentChapterIndex =
                chapterKey != null
                    ? Array.FindIndex(_chapters, c => c.Chapter.Id == chapterKey.Id)
                    : 0;

            var forceFirstPage = false;

            if (_currentChapterIndex == -1)
            {
                _currentChapterIndex = 0;
                _notificationService.ShowError(
                    "Selected chapter not found. Loading first chapter instead."
                );
            }
            else if (
                navigationFromContinueButton && _chapters[_currentChapterIndex].UserData.IsRead
            )
            {
                _currentChapterIndex = (_currentChapterIndex + 1) % _chapters.Length;
                forceFirstPage = true;
            }

            await UpdateCurrentChapter(forceFirstPage);
        }

        private async Task UpdateCurrentChapter(bool forceFirstPage = false)
        {
            if (
                _chapters == null
                || _comicKey == null
                || _currentChapterIndex < 0
                || _currentChapterIndex >= _chapters.Length
            )
                return;

            _chapterLoadCts.Cancel();
            _chapterLoadCts.Dispose();
            _chapterLoadCts = new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _chapterLoadCts.Token,
                _navigationCts.Token
            );

            IsLoading = true;

            CurrentChapter = _chapters[_currentChapterIndex].Chapter;
            ChapterTitle = CurrentChapter.ToDisplayTitle();

            var pagesResult = await _comicService.GetChapterPagesAsync(
                _comicKey,
                new(CurrentChapter.Id, CurrentChapter.Source),
                ct: linkedCts.Token
            );

            if (pagesResult.IsCancelled)
                return;

            if (pagesResult.IsSuccess)
            {
                ChapterPages = pagesResult
                    .Value!.Select(p => new ChapterPageItemViewModel(p, _displayContext))
                    .ToList();

                var chapterUserData = _chapters[_currentChapterIndex].UserData;
                CurrentPageIndex =
                    chapterUserData.LastPageRead > 0 && (!chapterUserData.IsRead && !forceFirstPage)
                        ? chapterUserData.LastPageRead.Value - 1
                        : 0;
            }
            else
            {
                await HandleNotSuccessResultAndNavigateBack(pagesResult);
            }

            IsLoading = false;
        }

        [RelayCommand]
        private async Task GoBack()
        {
            await SaveReadingProgressAsync();
            await SaveReaderSettingsAsync();
            _navigationCts.Cancel();
            _navigationCts.Dispose();

            _messenger.Send(new SetFullscreenMessage(false));
            _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));
        }

        private bool CanGoToNextChapter() =>
            _chapters != null && !IsLoading && _currentChapterIndex < _chapters.Length - 1;

        [RelayCommand(CanExecute = nameof(CanGoToNextChapter))]
        private async Task NextChapter()
        {
            await SaveReadingProgressAsync();

            _currentChapterIndex++;
            await UpdateCurrentChapter();
        }

        private bool CanGoToPreviousChapter() =>
            _chapters != null && !IsLoading && _currentChapterIndex > 0;

        [RelayCommand(CanExecute = nameof(CanGoToPreviousChapter))]
        private async Task PreviousChapter()
        {
            await SaveReadingProgressAsync();

            _currentChapterIndex--;
            await UpdateCurrentChapter();
        }

        private bool CanGoToNextPage() =>
            ChapterPages != null && CurrentPageIndex < ChapterPages.Count - 1;

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private void NextPage() => CurrentPageIndex++;

        private bool CanGoToPreviousPage() => ChapterPages != null && CurrentPageIndex > 0;

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private void PreviousPage() => CurrentPageIndex--;

        [RelayCommand]
        private void ToggleFullscreen() => _messenger.Send(new SetFullscreenMessage(IsFullscreen));

        [RelayCommand]
        private void SetReadingMode(string mode) => ReadingMode = Enum.Parse<ReadingMode>(mode);

        [RelayCommand]
        private void SetScalingMode(string mode) => ScalingMode = Enum.Parse<ScalingMode>(mode);

        [RelayCommand]
        private void SetScreenSize((double width, double height) size) =>
            _displayContext.ScreenSize = size;

        private void LoadReaderSettings()
        {
            if (_settingsService.Current.AutoFullscreen)
            {
                IsFullscreen = true;
                _messenger.Send(new SetFullscreenMessage(true));
            }

            ReadingMode = _settingsService.Current.ReadingMode;
            ScalingMode = _settingsService.Current.ScalingMode;
        }

        private async Task SaveReaderSettingsAsync()
        {
            _settingsService.Current.ReadingMode = ReadingMode;
            _settingsService.Current.ScalingMode = ScalingMode;
            await _settingsService.SaveAsync();
        }

        private async Task SaveReadingProgressAsync()
        {
            if (_comicKey == null || CurrentChapter == null || _chapters == null)
                return;

            var chapterUserData = _chapters[_currentChapterIndex].UserData;

            if (!chapterUserData.IsRead)
            {
                chapterUserData.LastPageRead = CurrentPageIndex + 1;
                chapterUserData.IsRead = CurrentChapter.Pages == chapterUserData.LastPageRead;

                var chapterProgressResult = await _comicService.UpsertChapterUserDataAsync(
                    _comicKey,
                    new ContentKey(CurrentChapter.Id, CurrentChapter.Source),
                    chapterUserData
                );

                if (!chapterProgressResult.IsSuccess)
                {
                    _notificationService.ShowError(chapterProgressResult.Error!);
                }
                else
                {
                    _messenger.Send(
                        new ChapterUserDataUpdatedMessage(
                            new ContentKey(CurrentChapter.Id, CurrentChapter.Source)
                        )
                    );
                }
            }

            var comicProgressResult = await _comicService.UpsertComicReadingProgressAsync(
                _comicKey,
                new() { LanguageCode = _language!, LastChapterId = CurrentChapter.Id }
            );

            if (!comicProgressResult.IsSuccess)
            {
                _notificationService.ShowError(comicProgressResult.Error!);
            }
        }

        private async Task HandleNotSuccessResultAndNavigateBack(Result result)
        {
            if (result.Kind == ResultKind.ComicSourceDisabled)
                await TriggerNotificationAndNavigateBack(() =>
                    _notificationService.ShowWarning(result.Error!, "ComicSource Disabled")
                );
            else
                await TriggerNotificationAndNavigateBack(() =>
                    _notificationService.ShowError(result.Error!, result.ErrorTitle!)
                );
        }

        private async Task TriggerNotificationAndNavigateBack(Action showNotification)
        {
            showNotification();
            await Task.Delay(1000);
            _messenger.Send(new SetFullscreenMessage(false));
            _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));
        }

        partial void OnScalingModeChanged(ScalingMode value) => _displayContext.ScalingMode = value;
    }
}
