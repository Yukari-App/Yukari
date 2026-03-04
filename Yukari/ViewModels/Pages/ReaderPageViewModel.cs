using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Helpers.UI;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class ReaderPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;
        private readonly INotificationService _notificationService;
        private readonly IMessenger _messenger;

        private ContentKey? _comicKey;
        private ChapterAggregate[]? _chapters;

        private int _currentChapterIndex = -1;
        private (double Width, double Height) ScreenSize = (0, 0);

        [ObservableProperty]
        public partial string? ComicTitle { get; set; }

        [ObservableProperty]
        public partial string? ChapterTitle { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(NextChapterCommand), nameof(PreviousChapterCommand))]
        public partial ChapterModel? CurrentChapter { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
        public partial List<ChapterPageItemViewModel>? ChapterPages { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
        public partial int CurrentPageIndex { get; set; } = 0;

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
            INotificationService notificationService,
            IMessenger messenger
        )
        {
            _comicService = comicService;
            _notificationService = notificationService;
            _messenger = messenger;
        }

        public async Task InitializeAsync(
            ContentKey comicKey,
            string comicTitle,
            ContentKey chapterKey,
            string selectedLang
        )
        {
            _comicKey = comicKey;
            ComicTitle = comicTitle;

            var result = await _comicService.GetAllChaptersAsync(comicKey, selectedLang);
            if (!result.IsSuccess)
            {
                await TriggerErrorAndReturn(result.Error!);
                return;
            }

            _chapters = result.Value!.ToArray();

            if (_chapters.Length <= 0)
            {
                await TriggerErrorAndReturn("No chapters found for this language.");
                return;
            }

            _currentChapterIndex = Array.FindIndex(_chapters, c => c.Chapter.Id == chapterKey.Id);

            if (_currentChapterIndex != -1)
                await UpdateCurrentChapter();
        }

        private async Task UpdateCurrentChapter()
        {
            if (
                _chapters == null
                || _currentChapterIndex < 0
                || _currentChapterIndex >= _chapters.Length
            )
                return;

            IsLoading = true;

            CurrentChapter = _chapters[_currentChapterIndex].Chapter;
            ChapterTitle = CurrentChapter.ToDisplayTitle();

            var pagesResult = await _comicService.GetChapterPagesAsync(
                _comicKey!,
                new(CurrentChapter.Id, CurrentChapter.Source)
            );
            if (pagesResult.IsSuccess)
            {
                ChapterPages = pagesResult
                    .Value!.Select(pageModel => new ChapterPageItemViewModel(pageModel)
                    {
                        ScreenSize = ScreenSize,
                        ScalingMode = ScalingMode,
                    })
                    .ToList();

                var chapterUserData = _chapters[_currentChapterIndex].UserData;
                CurrentPageIndex =
                    chapterUserData.LastPageRead > 0 && !chapterUserData.IsRead
                        ? chapterUserData.LastPageRead.Value - 1
                        : 0;
            }
            else
            {
                await TriggerErrorAndReturn(pagesResult.Error!);
            }

            IsLoading = false;
        }

        [RelayCommand]
        private async Task GoBack()
        {
            await SaveChapterProgress();
            _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));
        }

        private bool CanGoToNextChapter() =>
            _chapters != null && !IsLoading && _currentChapterIndex < _chapters.Length - 1;

        [RelayCommand(CanExecute = nameof(CanGoToNextChapter))]
        private async Task NextChapter()
        {
            await SaveChapterProgress();

            _currentChapterIndex++;
            await UpdateCurrentChapter();
        }

        private bool CanGoToPreviousChapter() =>
            _chapters != null && !IsLoading && _currentChapterIndex > 0;

        [RelayCommand(CanExecute = nameof(CanGoToPreviousChapter))]
        private async Task PreviousChapter()
        {
            await SaveChapterProgress();

            _currentChapterIndex--;
            await UpdateCurrentChapter();
        }

        private bool CanGoToNextPage() =>
            ChapterPages != null && CurrentPageIndex < ChapterPages.Count - 1;

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private async Task NextPage() => CurrentPageIndex++;

        private bool CanGoToPreviousPage() => ChapterPages != null && CurrentPageIndex > 0;

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private async Task PreviousPage() => CurrentPageIndex--;

        [RelayCommand]
        private void SetReadingMode(string mode) => ReadingMode = Enum.Parse<ReadingMode>(mode);

        [RelayCommand]
        private void SetScalingMode(string mode) => ScalingMode = Enum.Parse<ScalingMode>(mode);

        [RelayCommand]
        private void SetScreenSize((double width, double height) size)
        {
            ScreenSize = size;
            ChapterPages?.ForEach(page =>
            {
                page.ScreenSize = size;
            });
        }

        private async Task SaveChapterProgress()
        {
            if (CurrentChapter == null)
                return;

            var chapterUserData = _chapters![_currentChapterIndex].UserData;

            if (!chapterUserData.IsRead)
            {
                chapterUserData.LastPageRead = CurrentPageIndex + 1;
                chapterUserData.IsRead = CurrentChapter.Pages == chapterUserData.LastPageRead;

                var result = await _comicService.UpsertChapterUserDataAsync(
                    _comicKey!,
                    new ContentKey(CurrentChapter.Id, CurrentChapter.Source),
                    chapterUserData
                );

                if (!result.IsSuccess)
                {
                    _notificationService.ShowError(result.Error!);
                    return;
                }

                _messenger.Send(
                    new ChapterUserDataUpdatedMessage(
                        new ContentKey(CurrentChapter.Id, CurrentChapter.Source)
                    )
                );
            }
        }

        private async Task TriggerErrorAndReturn(string errorMessage)
        {
            _notificationService.ShowError(errorMessage);

            await Task.Delay(1000);
            _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));
        }

        /* When changing the ReadingMode, sometimes the view returns to the first page (although the index does not).
           I found this way to get around the problem, kind of reloading.
           It's good to try to find a better way to solve this problem in the future. #RefactorNeeded */
        async partial void OnReadingModeChanged(ReadingMode value)
        {
            var backupCurrentPage = CurrentPageIndex;

            CurrentPageIndex = -1;
            await Task.Delay(1);
            CurrentPageIndex = backupCurrentPage;
        }

        async partial void OnScalingModeChanged(ScalingMode value) =>
            ChapterPages?.ForEach(page => page.ScalingMode = value);
    }
}
