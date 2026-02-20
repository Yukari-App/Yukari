using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        [ObservableProperty] public partial string? ComicTitle { get; set; }
        [ObservableProperty] public partial string? ChapterTitle { get; set; }

        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(NextChapterCommand), nameof(PreviousChapterCommand))]
        public partial ChapterModel? CurrentChapter { get; set; }

        [ObservableProperty] public partial List<ChapterPageItemViewModel>? ChapterPages { get; set; }
        [ObservableProperty] public partial int CurrentPageIndex { get; set; } = 0;

        [ObservableProperty] public partial ReadingMode ReadingMode { get; set; } = ReadingMode.RightToLeft;
        [ObservableProperty] public partial ScalingMode ScalingMode { get; set; } = ScalingMode.FitScreen;


        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(NextChapterCommand), nameof(PreviousChapterCommand))]
        public partial bool IsLoading { get; set; } = true;

        public ReaderPageViewModel(IComicService comicService, INotificationService notificationService, IMessenger messenger)
        {
            _comicService = comicService;
            _notificationService = notificationService;
            _messenger = messenger;
        }

        public async Task InitializeAsync(ContentKey comicKey, string comicTitle, ContentKey chapterKey, string selectedLang)
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
            if (_chapters == null || _currentChapterIndex < 0 || _currentChapterIndex >= _chapters.Length)
                return;

            IsLoading = true;

            CurrentChapter = _chapters[_currentChapterIndex].Chapter;
            ChapterTitle = CurrentChapter.ToDisplayTitle();

            var pagesResult = await _comicService.GetChapterPagesAsync(_comicKey!, new(CurrentChapter.Id, CurrentChapter.Source));
            if (pagesResult.IsSuccess)
                ChapterPages = pagesResult.Value!.Select(pageModel => new ChapterPageItemViewModel(pageModel)).ToList();
            else
                _notificationService.ShowError("Failed to load chapter pages.");

            IsLoading = false;
        }

        [RelayCommand]
        public void GoBack() => _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));

        private bool CanGoToNext() => _chapters != null && !IsLoading && _currentChapterIndex < _chapters.Length - 1;

        [RelayCommand(CanExecute = nameof(CanGoToNext))]
        public async Task NextChapter()
        {
            _currentChapterIndex++;
            await UpdateCurrentChapter();
        }

        private bool CanGoToPrevious() => _chapters != null && !IsLoading && _currentChapterIndex > 0;

        [RelayCommand(CanExecute = nameof(CanGoToPrevious))]
        public async Task PreviousChapter()
        {
            _currentChapterIndex--;
            await UpdateCurrentChapter();
        }

        [RelayCommand] private void SetReadingMode(string mode) => ReadingMode = Enum.Parse<ReadingMode>(mode);
        [RelayCommand] private void SetScalingMode(string mode) => ScalingMode = Enum.Parse<ScalingMode>(mode);

        private async Task TriggerErrorAndReturn(string errorMessage)
        {
            _notificationService.ShowError(errorMessage);

            await Task.Delay(1000);
            _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));
        }

        async partial void OnReadingModeChanged(ReadingMode value)
        {
            var backupPages = ChapterPages;
            var backupCurrentPage = CurrentPageIndex;

            ChapterPages = null;
            ChapterPages = backupPages;

            await Task.Delay(1); // Force UI to refresh the collection
            CurrentPageIndex = backupCurrentPage;
        }
    }
}
