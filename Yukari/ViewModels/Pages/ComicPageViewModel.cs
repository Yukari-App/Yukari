using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Core.Models;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;
using Yukari.Views.Pages;

namespace Yukari.ViewModels.Pages;

public partial class ComicPageViewModel
    : ObservableObject,
        IRecipient<ChapterUserDataUpdatedMessage>
{
    private const int MaxTagsToShow = 16;

    private readonly IComicService _comicService;
    private readonly IDownloadService _downloadService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IMessenger _messenger;
    private readonly ILocalizationService _localizationService;

    private ContentKey? _comicKey;

    private CancellationTokenSource _navigationCts = new();
    private CancellationTokenSource _chaptersCts = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsLocalComic),
        nameof(IsComicAvailable),
        nameof(IsOnlineFeaturesVisible),
        nameof(AuthorDisplay),
        nameof(IsPublishedVisible),
        nameof(StatusDisplay),
        nameof(IsTagsVisible),
        nameof(DisplayTags),
        nameof(HasHiddenTags),
        nameof(HiddenTagsText)
    )]
    public partial ComicModel? Comic { get; set; }

    public string AuthorDisplay => Comic?.Author ?? _localizationService.GetString("Unknown");
    public bool IsPublishedVisible => Comic?.Year != null;

    public string StatusDisplay =>
        Comic?.Status switch
        {
            ComicStatus.Ongoing => _localizationService.GetString("ComicStatus/Ongoing"),
            ComicStatus.Completed => _localizationService.GetString("ComicStatus/Completed"),
            ComicStatus.Hiatus => _localizationService.GetString("ComicStatus/Hiatus"),
            ComicStatus.Cancelled => _localizationService.GetString("ComicStatus/Cancelled"),
            _ => _localizationService.GetString("Unknown"),
        };

    public bool IsTagsVisible => Comic?.Tags.Length > 0;

    public IEnumerable<string> DisplayTags =>
        Comic?.Tags.Take(MaxTagsToShow) ?? Enumerable.Empty<string>();

    public bool HasHiddenTags => Comic?.Tags.Length > MaxTagsToShow;
    public string HiddenTagsText =>
        HasHiddenTags
            ? _localizationService.GetFormattedString(
                "HiddenTags",
                Comic!.Tags.Length - MaxTagsToShow
            )
            : string.Empty;

    [ObservableProperty]
    public partial List<ChapterItemViewModel>? Chapters { get; set; }

    [ObservableProperty]
    public partial string? SelectedLang { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnlineFeaturesVisible), nameof(FavoriteIcon))]
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
    [NotifyPropertyChangedFor(nameof(IsComicLoading), nameof(IsComicError), nameof(IsComicLoaded))]
    [NotifyCanExecuteChangedFor(
        nameof(ContinueReadingCommand),
        nameof(ToggleFavoriteCommand),
        nameof(UpdateCommand),
        nameof(ToggleDownloadAllChaptersCommand)
    )]
    public partial LoadState ComicLoadState { get; set; } = LoadState.Loading;

    [ObservableProperty]
    public partial string? ComicErrorTitle { get; set; }

    [ObservableProperty]
    public partial string? ComicErrorMessage { get; set; }

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

    public bool IsComicLoading => ComicLoadState == LoadState.Loading;
    public bool IsComicError => ComicLoadState == LoadState.Error;
    public bool IsComicLoaded => ComicLoadState == LoadState.Loaded;

    public bool IsLocalComic => Comic?.IsLocal() ?? false;
    public bool IsComicAvailable => Comic?.IsAvailable ?? true;
    public bool NoChapters => !IsChaptersLoading && (Chapters == null || Chapters.Count == 0);
    public bool IsInterfaceReady => !IsFavoriteStatusChanging && !IsChaptersLoading && !NoChapters;

    public bool IsOnlineFeaturesVisible => IsFavorite && !IsLocalComic;
    public bool IsLanguageSelectionAvailable =>
        !IsLocalComic && !IsFavoriteStatusChanging && !IsChaptersLoading && Comic?.Langs.Length > 0;

    public string FavoriteIcon => IsFavorite ? "\uE8D9" : "\uE734";
    public string DownloadAllIcon => IsAllChaptersDownloaded ? "\uE74D" : "\uE896";
    public string DownloadAllText =>
        _localizationService.GetString(
            IsAllChaptersDownloaded ? "DeleteAllChapters" : "DownloadAllChapters"
        );

    public ComicPageViewModel(
        IComicService comicService,
        IDownloadService downloadService,
        IDialogService dialogService,
        INotificationService notificationService,
        IMessenger messenger,
        ILocalizationService localizationService
    )
    {
        _comicService = comicService;
        _downloadService = downloadService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _messenger = messenger;
        _localizationService = localizationService;

        _messenger.RegisterAll(this);
    }

    public void Receive(ChapterUserDataUpdatedMessage message) =>
        Chapters
            ?.FirstOrDefault(c => c.Key.Equals(message.ChapterKey))
            ?.RefreshUserDataAsync(message.totalPages);

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

        foreach (var chapter in Chapters ?? Enumerable.Empty<ChapterItemViewModel>())
            chapter.PropertyChanged -= OnChapterDownloadStatusChanged;
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
        {
            result = await _comicService.RemoveFavoriteComicAsync(_comicKey);
            if (result.IsSuccess && IsLocalComic)
            {
                _messenger.Send(new NavigateMessage(typeof(FavoritesPage), null));
                return;
            }
        }

        if (result.IsSuccess)
            await RefreshChaptersAsync();
        else
        {
            IsFavorite = !newState;
            HandleNotSuccessResult(result);
        }

        IsFavoriteStatusChanging = false;
    }

    private bool CanUpdate() => Comic != null && !IsChaptersLoading && !IsLocalComic;

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

        _notificationService.ShowSuccess(_localizationService.GetString("SuccessUpdatingComic"));
    }

    [RelayCommand]
    private async Task OpenLocalComicManagerAsync()
    {
        if (_comicKey == null || Comic == null || !Comic.IsLocal())
            return;

        await _dialogService.ShowLocalComicDialogAsync(_comicKey);

        await RefreshComicAsync();
        await RefreshChaptersAsync();
    }

    [RelayCommand]
    private async Task OpenComicCollectionsManager()
    {
        if (_comicKey == null || Comic == null)
            return;

        await _dialogService.ShowComicCollectionsDialogAsync(_comicKey, Comic.Title);
    }

    private bool CanOpenInBrowser() => !string.IsNullOrEmpty(Comic?.ComicUrl) && !IsLocalComic;

    [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
    private async Task OpenInBrowserAsync() =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri(Comic!.ComicUrl!));

    private bool CanToggleDownloadAllChapters() => IsFavorite && IsInterfaceReady && !IsLocalComic;

    [RelayCommand(CanExecute = nameof(CanToggleDownloadAllChapters))]
    private async Task ToggleDownloadAllChaptersAsync()
    {
        if (_comicKey == null || Chapters == null || Chapters.Count == 0)
            return;

        if (IsAllChaptersDownloaded)
        {
            var chaptersToDelete = Chapters!.Where(c =>
                c.IsDownloaded || c.IsDownloading || c.IsDownloadQueued
            );
            foreach (var chapterVm in chaptersToDelete)
                await _downloadService.DeleteChapterDownloadAsync(_comicKey, chapterVm.Key);
        }
        else
        {
            var chaptersToDownload = Chapters!.Where(c =>
                !c.IsDownloaded && !c.IsDownloading && !c.IsDownloadQueued
            );
            foreach (var chapterVm in chaptersToDownload)
                _downloadService.EnqueueChapterDownload(
                    _comicKey,
                    chapterVm.Key,
                    Comic!.Title,
                    chapterVm.DisplayTitle!,
                    ct =>
                        _comicService.GetChapterPagesAsync(
                            _comicKey,
                            chapterVm.Key,
                            forceWeb: true,
                            ct
                        )
                );
        }

        await RefreshChaptersAsync();
    }

    [RelayCommand]
    private async Task RescanChaptersAsync()
    {
        if (_comicKey == null || Comic?.ComicUrl == null)
            return;

        var result = await _comicService.RescanLocalChaptersAsync(_comicKey, Comic.ComicUrl);

        if (!result.IsSuccess)
        {
            HandleNotSuccessResult(result);
            return;
        }
        await RefreshChaptersAsync();
        _notificationService.ShowSuccess(
            _localizationService.GetString("SuccessChaptersRescanned")
        );
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
        ComicLoadState = LoadState.Loading;

        var result = await _comicService.GetComicDetailsAsync(_comicKey, ct: _navigationCts.Token);

        if (result.IsCancelled)
            return;

        if (!result.IsSuccess)
        {
            Comic = null;
            ComicLoadState = LoadState.Error;
            ComicErrorTitle = result.ErrorTitle;
            ComicErrorMessage = result.Error;
            return;
        }

        var comicAggregate = result.Value;
        if (comicAggregate == null)
        {
            Comic = null;
            ComicLoadState = LoadState.Error;
            ComicErrorTitle = _localizationService.GetString("ErrorComicNotFoundTitle");
            ComicErrorMessage = _localizationService.GetString("ErrorComicNotFoundMessage");
            return;
        }

        Comic = comicAggregate.Comic;

        var userData = comicAggregate.UserData;
        IsFavorite = userData.IsFavorite;
        SelectedLang = userData.LastSelectedLang ?? Comic.Langs.FirstOrDefault()?.Key;

        ComicLoadState = LoadState.Loaded;
    }

    private async Task RefreshChaptersAsync()
    {
        if (!IsComicLoaded || _comicKey == null)
            return;

        _chaptersCts.Cancel();
        _chaptersCts.Dispose();
        _chaptersCts = new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _chaptersCts.Token,
            _navigationCts.Token
        );

        IsChaptersLoading = true;

        foreach (var chapter in Chapters ?? Enumerable.Empty<ChapterItemViewModel>())
            chapter.PropertyChanged -= OnChapterDownloadStatusChanged;

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
                ?.Select(c =>
                {
                    var ivm = new ChapterItemViewModel(
                        _comicService,
                        _downloadService,
                        _notificationService,
                        c,
                        _comicKey,
                        IsFavorite,
                        Comic!.Title,
                        NavigateToReaderCommand,
                        MarkPreviousChaptersAsReadCommand
                    );
                    ivm.PropertyChanged += OnChapterDownloadStatusChanged;
                    return ivm;
                })
                .ToList();
        }
        else
        {
            Chapters = null;
            HandleNotSuccessResult(result);
        }

        UpdateIsAllChaptersDownloaded();
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
        var result = await _comicService.UpsertChaptersIsReadAsync(_comicKey!, chapterIDs, isRead);
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
            _notificationService.ShowWarning(
                result.Error!,
                _localizationService.GetString("WarningSourceDisabled")
            );
        else if (!result.IsSuccess)
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
    }

    private void UpdateIsAllChaptersDownloaded()
    {
        IsAllChaptersDownloaded =
            Chapters is { Count: > 0 }
            && !Chapters.Any(c => !c.IsDownloaded && !c.IsDownloading && !c.IsDownloadQueued);
    }

    private void OnChapterDownloadStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName is nameof(ChapterItemViewModel.IsDownloaded)
            || e.PropertyName == nameof(ChapterItemViewModel.IsDownloading)
        )
        {
            UpdateIsAllChaptersDownloaded();
        }
    }

    async partial void OnSelectedLangChanged(string? value)
    {
        if (!IsComicLoaded)
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
