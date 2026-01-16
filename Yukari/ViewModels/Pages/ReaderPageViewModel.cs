using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Helpers.UI;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
namespace Yukari.ViewModels.Pages
{
    public partial class ReaderPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;
        private readonly IMessenger _messenger;

        private ContentKey? _comicKey;
        private ChapterAggregate[]? _chapters;

        private int _currentChapterIndex = -1;

        [ObservableProperty] public partial string? ComicTitle { get; set; }
        [ObservableProperty] public partial string? ChapterTitle { get; set; }

        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(NextChapterCommand), nameof(PreviousChapterCommand))]
        public partial ChapterModel? CurrentChapter { get; set; }


        public ReaderPageViewModel(IComicService comicService, IMessenger messenger)
        {
            _comicService = comicService;
            _messenger = messenger;
        }

        public async Task InitializeAsync(ContentKey comicKey, string comicTitle, ContentKey chapterKey, string selectedLang)
        {
            _comicKey = comicKey;
            ComicTitle = comicTitle;

            _chapters = (await _comicService.GetAllChaptersAsync(comicKey, selectedLang)).ToArray();
            _currentChapterIndex = Array.FindIndex(_chapters, c => c.Chapter.Id == chapterKey.Id);

            if (_currentChapterIndex != -1)
                UpdateCurrentChapter();
        }

        private void UpdateCurrentChapter()
        {
            if (_chapters == null || _currentChapterIndex < 0 || _currentChapterIndex >= _chapters.Length)
                return;

            CurrentChapter = _chapters[_currentChapterIndex].Chapter;
            ChapterTitle = CurrentChapter.ToDisplayTitle();

            // TO-DO: Load pages for the current chapter
            // LoadPagesAsync(CurrentChapter.Id);
        }

        [RelayCommand]
        public void GoBack() => _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));

        private bool CanGoToNext() => _chapters != null && _currentChapterIndex < _chapters.Length - 1;

        [RelayCommand(CanExecute = nameof(CanGoToNext))]
        public void NextChapter()
        {
            _currentChapterIndex++;
            UpdateCurrentChapter();
        }

        private bool CanGoToPrevious() => _currentChapterIndex > 0;

        [RelayCommand(CanExecute = nameof(CanGoToPrevious))]
        public void PreviousChapter()
        {
            _currentChapterIndex--;
            UpdateCurrentChapter();
        }
    }
}
