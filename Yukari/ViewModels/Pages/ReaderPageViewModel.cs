using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Helpers.UI;
using Yukari.Messages;
using Yukari.Messages.Shortcuts;
using Yukari.Models;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages;

public partial class ReaderPageViewModel : ObservableObject, IRecipient<FullscreenShortcutMessage>
{
    public const int PageProgressDebounceMs = 600;

    private readonly IComicService _comicService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;
    private readonly IImageCacheService _imageCacheService;
    private readonly IMessenger _messenger;
    private readonly ILocalizationService _localizationService;

    private readonly ReaderDisplayContext _displayContext = new();

    private ContentKey? _comicKey;
    private string? _language;
    private ChapterAggregate[]? _chapters;

    private int _currentChapterIndex = -1;
    private bool _isDisposed;

    private CancellationTokenSource _navigationCts = new();
    private CancellationTokenSource _chapterLoadCts = new();
    private CancellationTokenSource? _pageProgressDebounceCts;
    private readonly SemaphoreSlim _saveProgressLock = new(1, 1);

    public bool IsSyncingPageIndex { get; set; }

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

    // WebtoonPageIndex is separate from CurrentPageIndex because Webtoon mode updates the index
    // at a very high frequency during scrolling. Writing directly to CurrentPageIndex would flood
    // the TwoWay-bound FlipView (even when invisible), corrupting its internal state. Sync between
    // the two only happens on reading mode change (OnReadingModeChanged).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageIndicatorText))]
    public partial int WebtoonPageIndex { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsPositioningWebtoonScroll { get; set; }

    public int CurrentPageForDisplay => GetCurrentPageIndex() + 1;

    public string PageIndicatorText =>
        ChapterPages?.Count > 0 ? $"{CurrentPageForDisplay} / {ChapterPages.Count}" : "0 / 0";

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
        nameof(IsVerticalPageNumberIndicatorVisible),
        nameof(ForwardPageNavigationButtonCommand),
        nameof(BackwardPageNavigationButtonCommand),
        nameof(IsDefaultReaderVisible),
        nameof(IsWebtoonReaderVisible)
    )]
    public partial ReadingMode ReadingMode { get; set; } = ReadingMode.RightToLeft;

    public IRelayCommand ForwardChapterNavigationButtonCommand =>
        ReadingMode == ReadingMode.RightToLeft ? NextChapterCommand : PreviousChapterCommand;

    public IRelayCommand BackwardChapterNavigationButtonCommand =>
        ReadingMode == ReadingMode.RightToLeft ? PreviousChapterCommand : NextChapterCommand;

    public string ForwardChapterNavigationButtonToolTip =>
        _localizationService.GetString(
            ReadingMode == ReadingMode.RightToLeft ? "NextChapter" : "PreviousChapter"
        );

    public string BackwardChapterNavigationButtonToolTip =>
        _localizationService.GetString(
            ReadingMode == ReadingMode.RightToLeft ? "PreviousChapter" : "NextChapter"
        );

    public bool IsHorizontalPageNavigationButtonsVisible =>
        ReadingMode is ReadingMode.RightToLeft or ReadingMode.LeftToRight;

    public bool IsVerticalPageNavigationButtonsVisible => ReadingMode == ReadingMode.Vertical;
    public bool IsVerticalPageNumberIndicatorVisible =>
        ReadingMode is ReadingMode.Vertical or ReadingMode.Webtoon;

    public IRelayCommand ForwardPageNavigationButtonCommand =>
        ReadingMode == ReadingMode.RightToLeft ? NextPageCommand : PreviousPageCommand;

    public IRelayCommand BackwardPageNavigationButtonCommand =>
        ReadingMode == ReadingMode.RightToLeft ? PreviousPageCommand : NextPageCommand;

    [ObservableProperty]
    public partial ScalingMode ScalingMode { get; set; } = ScalingMode.FitScreen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsLoading),
        nameof(IsLoaded),
        nameof(IsError),
        nameof(IsDefaultReaderVisible),
        nameof(IsWebtoonReaderVisible)
    )]
    [NotifyCanExecuteChangedFor(
        nameof(NextChapterCommand),
        nameof(PreviousChapterCommand),
        nameof(SaveCurrentPageImageCommand)
    )]
    public partial LoadState ChapterState { get; set; } = LoadState.Loading;

    [ObservableProperty]
    public partial bool IsWarning { get; set; } = false;

    [ObservableProperty]
    public partial string? ChapterErrorTitle { get; set; }

    [ObservableProperty]
    public partial string? ChapterErrorMessage { get; set; }

    public bool IsLoading => ChapterState == LoadState.Loading;
    public bool IsLoaded => ChapterState == LoadState.Loaded;
    public bool IsError => ChapterState == LoadState.Error;

    public bool IsDefaultReaderVisible => IsLoaded && ReadingMode != ReadingMode.Webtoon;
    public bool IsWebtoonReaderVisible => IsLoaded && ReadingMode == ReadingMode.Webtoon;

    public ReaderPageViewModel(
        IComicService comicService,
        ISettingsService settingsService,
        INotificationService notificationService,
        IDialogService dialogService,
        IImageCacheService imageCacheService,
        IMessenger messenger,
        ILocalizationService localizationService
    )
    {
        _comicService = comicService;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _dialogService = dialogService;
        _imageCacheService = imageCacheService;
        _messenger = messenger;
        _localizationService = localizationService;

        _messenger.RegisterAll(this);
        LoadReaderSettings();
    }

    public void Receive(FullscreenShortcutMessage message)
    {
        IsFullscreen = !IsFullscreen;
        _messenger.Send(new SetFullscreenMessage(IsFullscreen));
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
            _settingsService.Current.HideDuplicateChapters,
            ct: _navigationCts.Token
        );

        if (result.IsCancelled)
            return;

        if (!result.IsSuccess)
        {
            ChapterState = LoadState.Error;
            IsWarning = result.Kind == ResultKind.ComicSourceDisabled;
            ChapterErrorTitle = IsWarning
                ? _localizationService.GetString("WarningSourceDisabled")
                : result.ErrorTitle;
            ChapterErrorMessage = result.Error;
            return;
        }

        _chapters = result.Value!.Where(c => c.IsReadable).ToArray();

        if (_chapters.Length == 0)
        {
            ChapterState = LoadState.Error;
            ChapterErrorTitle = _localizationService.GetString("ErrorNoChaptersFoundTitle");
            ChapterErrorMessage = _localizationService.GetString("ErrorNoChaptersFoundMessage");
            return;
        }

        _currentChapterIndex =
            chapterKey != null ? Array.FindIndex(_chapters, c => c.Chapter.Id == chapterKey.Id) : 0;

        var forceFirstPage = false;

        if (_currentChapterIndex == -1)
        {
            _currentChapterIndex = 0;
            _notificationService.ShowWarning(
                _localizationService.GetString("WarningSelectedChapterNotFound")
            );
        }
        else if (navigationFromContinueButton && _chapters[_currentChapterIndex].UserData.IsRead)
        {
            _currentChapterIndex = (_currentChapterIndex + 1) % _chapters.Length;
            forceFirstPage = true;
        }

        await UpdateCurrentChapter(forceFirstPage);
    }

    public async Task InitializeAsync(string filePath)
    {
        _comicKey = null;
        _language = null;
        _chapters = null;
        _currentChapterIndex = -1;

        ComicTitle = Path.GetFileNameWithoutExtension(filePath);
        ChapterTitle = null;
        ChapterState = LoadState.Loading;

        var result = await _comicService.GetStandaloneChapterAsync(filePath, _navigationCts.Token);

        if (result.IsCancelled)
            return;

        if (!result.IsSuccess)
        {
            ChapterState = LoadState.Error;
            IsWarning = false;
            ChapterErrorTitle = result.ErrorTitle;
            ChapterErrorMessage = result.Error;
            return;
        }

        var scanResult = result.Value;
        if (scanResult == null)
        {
            ChapterState = LoadState.Error;
            IsWarning = false;
            ChapterErrorTitle = _localizationService.GetString("ErrorNoChaptersFoundTitle");
            ChapterErrorMessage = _localizationService.GetString("ErrorNoChaptersFoundMessage");
            return;
        }

        CurrentChapter = scanResult.Chapter;
        ComicTitle = CurrentChapter.ToDisplayTitle();

        ChapterPages = scanResult
            .Pages.Select(p => new ChapterPageItemViewModel(_imageCacheService, p, _displayContext))
            .ToList();

        CurrentPageIndex = WebtoonPageIndex = 0;

        if (ChapterPages.Count == 0)
        {
            ChapterState = LoadState.Error;
            IsWarning = true;
            ChapterErrorTitle = _localizationService.GetString("ErrorEmptyChapterPagesTitle");
            ChapterErrorMessage = _localizationService.GetString("ErrorEmptyChapterPagesMessage");
            return;
        }

        ChapterState = LoadState.Loaded;
    }

    public void OnNavigatedFrom()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _pageProgressDebounceCts?.Cancel();
        _pageProgressDebounceCts?.Dispose();
        _pageProgressDebounceCts = null;

        _ = SaveChapterProgressAsync();

        _chapterLoadCts.Cancel();
        _chapterLoadCts.Dispose();
        _navigationCts.Cancel();
        _navigationCts.Dispose();
        _messenger.UnregisterAll(this);
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

        ChapterState = LoadState.Loading;

        CurrentChapter = _chapters[_currentChapterIndex].Chapter;
        ChapterTitle = CurrentChapter.ToDisplayTitle();

        var pagesResult = await _comicService.GetChapterPagesAsync(
            _comicKey,
            CurrentChapter.Key,
            ct: linkedCts.Token
        );

        if (pagesResult.IsCancelled)
            return;

        if (!pagesResult.IsSuccess)
        {
            ChapterState = LoadState.Error;
            IsWarning = pagesResult.Kind == ResultKind.ComicSourceDisabled;
            ChapterErrorTitle = IsWarning
                ? _localizationService.GetString("WarningSourceDisabled")
                : pagesResult.ErrorTitle;
            ChapterErrorMessage = pagesResult.Error;
            return;
        }

        ChapterPages = pagesResult
            .Value!.Select(p => new ChapterPageItemViewModel(
                _imageCacheService,
                p,
                _displayContext
            ))
            .ToList();

        var chapterUserData = _chapters[_currentChapterIndex].UserData;
        CurrentPageIndex = WebtoonPageIndex = chapterUserData.ResumePageIndex(forceFirstPage);

        // Ensure a page count for chapters, even when the Source does not provide it.
        if (CurrentChapter.Pages != ChapterPages.Count)
        {
            CurrentChapter.Pages = ChapterPages.Count;
            _ = _comicService.UpdateChapterPageCountAsync(
                _comicKey,
                CurrentChapter.Key,
                ChapterPages.Count
            );
        }

        if (ChapterPages.Count == 0)
        {
            ChapterState = LoadState.Error;
            IsWarning = true;
            ChapterErrorTitle = _localizationService.GetString("ErrorEmptyChapterPagesTitle");
            ChapterErrorMessage = _localizationService.GetString("ErrorEmptyChapterPagesMessage");
            return;
        }

        ChapterState = LoadState.Loaded;
        await SaveComicProgressAsync();
    }

    [RelayCommand]
    private void GoBack()
    {
        _messenger.Send(new SetFullscreenMessage(false));
        _messenger.Send(new SwitchAppModeMessage(AppMode.Navigation));
    }

    private bool CanGoToNextChapter() =>
        _chapters != null && !IsLoading && _currentChapterIndex < _chapters.Length - 1;

    [RelayCommand(CanExecute = nameof(CanGoToNextChapter))]
    private async Task NextChapter()
    {
        await SaveChapterProgressAsync();

        _currentChapterIndex++;
        await UpdateCurrentChapter();
    }

    private bool CanGoToPreviousChapter() =>
        _chapters != null && !IsLoading && _currentChapterIndex > 0;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousChapter))]
    private async Task PreviousChapter()
    {
        await SaveChapterProgressAsync();

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
    private void JumpToPage(int pageIndex)
    {
        if (ChapterPages == null || pageIndex < 0 || pageIndex >= ChapterPages.Count)
            return;
        CurrentPageIndex = pageIndex;
    }

    [RelayCommand]
    private void ToggleFullscreen() => _messenger.Send(new SetFullscreenMessage(IsFullscreen));

    [RelayCommand]
    private async Task SetReadingMode(string mode)
    {
        ReadingMode = Enum.Parse<ReadingMode>(mode);
        _settingsService.Set(s => s.ReadingMode, ReadingMode);
        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private async Task SetScalingMode(string mode)
    {
        ScalingMode = Enum.Parse<ScalingMode>(mode);
        _settingsService.Set(s => s.ScalingMode, ScalingMode);
        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private void SetScreenSize((double width, double height) size) =>
        _displayContext.ScreenSize = size;

    private bool CanSaveCurrentPageImage() => IsLoaded && ChapterPages != null;

    [RelayCommand(CanExecute = nameof(CanSaveCurrentPageImage))]
    private async Task SaveCurrentPageImageAsync()
    {
        if (ChapterPages == null)
            return;
        var currentPageImageUrl = ChapterPages[GetCurrentPageIndex()].ImageUrl;

        var ext = ImageUriHelper.GetFileExtension(currentPageImageUrl);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".jpg";

        var titlePrefix = string.IsNullOrWhiteSpace(ChapterTitle)
            ? ComicTitle
            : $"{ComicTitle} - {ChapterTitle}";

        var filePath = await _dialogService.OpenFileSavePicker(
            FileHelper.SanitizeFileName($"{titlePrefix} - Page {CurrentPageForDisplay}"),
            new Dictionary<string, string[]> { { ext, new[] { ext } } }
        );
        if (filePath == null)
            return;

        filePath = await _imageCacheService.SaveImageAsync(currentPageImageUrl, filePath);
        if (filePath != null)
            _notificationService.ShowSuccess(
                _localizationService.GetString("SuccessSavingCurrentPageImage")
            );
        else
            _notificationService.ShowError(
                _localizationService.GetString("ErrorSavingCurrentPageImageMessage")
            );
    }

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

    private async Task SaveComicProgressAsync()
    {
        if (_comicKey == null || CurrentChapter == null)
            return;

        var comicProgressResult = await _comicService.UpsertComicReadingProgressAsync(
            _comicKey,
            new() { LanguageCode = _language!, LastChapterId = CurrentChapter.Id }
        );

        if (!comicProgressResult.IsSuccess)
            _notificationService.ShowError(comicProgressResult.Error!);
    }

    private async Task SaveChapterProgressAsync()
    {
        if (!IsLoaded || _comicKey == null || CurrentChapter == null || _chapters == null)
            return;
        _pageProgressDebounceCts?.Cancel();

        var chapter = CurrentChapter;
        var userData = _chapters[_currentChapterIndex].UserData;
        var page = ChapterPages?.Count > 0 ? GetCurrentPageIndex() + 1 : 0;
        var totalPages = ChapterPages?.Count;

        await _saveProgressLock.WaitAsync();
        try
        {
            if (!userData.IsRead)
            {
                userData.RecordProgress(page, chapter.Pages);

                var chapterProgressResult = await _comicService.UpsertChapterUserDataAsync(
                    _comicKey,
                    chapter.Key,
                    userData
                );

                if (!chapterProgressResult.IsSuccess)
                    _notificationService.ShowError(chapterProgressResult.Error!);
            }

            _messenger.Send(new ChapterUserDataUpdatedMessage(chapter.Key, totalPages));
        }
        finally
        {
            _saveProgressLock.Release();
        }
    }

    private async Task SavePageProgressDebouncedAsync()
    {
        if (_comicKey == null || CurrentChapter == null || _chapters == null)
            return;

        if (_chapters[_currentChapterIndex].UserData.IsRead)
            return;

        _pageProgressDebounceCts?.Cancel();
        _pageProgressDebounceCts?.Dispose();
        _pageProgressDebounceCts = new CancellationTokenSource();

        var ct = _pageProgressDebounceCts.Token;
        try
        {
            await Task.Delay(PageProgressDebounceMs, ct);
            await SaveChapterProgressAsync();
        }
        catch (TaskCanceledException) { }
    }

    private int GetCurrentPageIndex() =>
        ReadingMode == ReadingMode.Webtoon ? WebtoonPageIndex : CurrentPageIndex;

    partial void OnCurrentPageIndexChanged(int value)
    {
        if (!IsLoaded || IsSyncingPageIndex || ReadingMode == ReadingMode.Webtoon)
            return;
        _ = SavePageProgressDebouncedAsync();
    }

    partial void OnWebtoonPageIndexChanged(int value)
    {
        if (!IsLoaded || IsSyncingPageIndex || ReadingMode != ReadingMode.Webtoon)
            return;
        _ = SavePageProgressDebouncedAsync();
    }

    partial void OnReadingModeChanged(ReadingMode oldValue, ReadingMode newValue)
    {
        if (!IsLoaded)
            return;

        IsSyncingPageIndex = true;

        if (oldValue == ReadingMode.Webtoon)
            CurrentPageIndex = WebtoonPageIndex;
        else
            WebtoonPageIndex = CurrentPageIndex;

        IsSyncingPageIndex = false;
    }

    partial void OnScalingModeChanged(ScalingMode value) => _displayContext.ScalingMode = value;
}
