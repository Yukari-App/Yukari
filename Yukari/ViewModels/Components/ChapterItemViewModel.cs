using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Helpers.UI;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Storage;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Components;

public partial class ChapterItemViewModel : ObservableObject
{
    private const int MaxGroupsToShow = 3;

    private readonly IComicService _comicService;
    private readonly IDownloadService _downloadService;
    private readonly INotificationService _notificationService;

    private readonly ContentKey _comicKey;
    private readonly bool _isComicFavorite;
    private readonly string _comicTitle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsDownloadQueued),
        nameof(IsDownloading),
        nameof(IsDownloadCancelled),
        nameof(IsDownloadFailed),
        nameof(DownloadButtonValue),
        nameof(DownloadIcon),
        nameof(IsChapterAvailable),
        nameof(IsDownloadAvailable)
    )]
    private partial DownloadItem? DownloadItem { get; set; }

    public IRelayCommand<ContentKey>? NavigateToReaderCommand { get; }
    public IRelayCommand<ChapterItemViewModel>? MarkPreviousChaptersAsReadCommand { get; }

    public ChapterModel Chapter { get; }
    public ContentKey Key => new(Chapter.Id, Chapter.Source);

    public string? DisplayTitle { get; }

    public IEnumerable<string> DisplayGroups => Chapter.Groups.Take(MaxGroupsToShow);

    public bool HasMoreGroups => Chapter.Groups.Length > MaxGroupsToShow;
    public string ExtraGroupsText =>
        HasMoreGroups ? $"+{Chapter.Groups.Length - MaxGroupsToShow}" : string.Empty;

    [ObservableProperty]
    public partial int? LastPageRead { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsDownloadQueued),
        nameof(IsDownloading),
        nameof(IsDownloadCancelled),
        nameof(IsDownloadFailed),
        nameof(DownloadButtonValue),
        nameof(DownloadIcon),
        nameof(IsChapterAvailable),
        nameof(IsDownloadAvailable)
    )]
    public partial bool IsDownloaded { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ReadIcon))]
    public partial bool IsRead { get; set; }

    public bool IsDownloadQueued => DownloadItem?.Status == DownloadStatus.Queued;
    public bool IsDownloading => DownloadItem?.Status == DownloadStatus.Downloading;
    public bool IsDownloadCancelled => DownloadItem?.Status == DownloadStatus.Cancelled;
    public bool IsDownloadFailed => DownloadItem?.Status == DownloadStatus.Failed;

    public double DownloadProgress => DownloadItem?.Progress ?? 0.0;
    public string FormattedDownloadProgress => DownloadItem?.FormattedProgress ?? "00%";

    public bool IsChapterAvailable => Chapter.IsAvailable || IsDownloaded;
    public bool IsDownloadAvailable => _isComicFavorite && IsChapterAvailable;

    public bool DownloadButtonValue => IsDownloaded || IsDownloadQueued || IsDownloading; // The download ToggleButton is checked when the chapter is either downloaded or currently downloading
    public string DownloadIcon =>
        IsDownloaded ? "\uE74D"
        : IsDownloadQueued || IsDownloading ? "\uF78A"
        : IsDownloadCancelled || IsDownloadFailed ? "\uE72C"
        : "\uE896";
    public string ReadIcon => IsRead ? "\uED1A" : "\uE890";

    public ChapterItemViewModel(
        IComicService comicService,
        IDownloadService downloadService,
        INotificationService notificationService,
        ChapterAggregate chapterAggregate,
        ContentKey comicKey,
        bool isComicFavorite,
        string comicTitle,
        IRelayCommand<ContentKey> navigateToReaderCommand,
        IRelayCommand<ChapterItemViewModel> markPreviousChaptersAsReadCommand
    )
    {
        _comicService = comicService;
        _downloadService = downloadService;
        _notificationService = notificationService;

        _comicKey = comicKey;
        Chapter = chapterAggregate.Chapter;
        _isComicFavorite = isComicFavorite;
        _comicTitle = comicTitle;

        DisplayTitle = Chapter.ToDisplayTitle();

        var chapterUserData = chapterAggregate.UserData;
        IsDownloaded = chapterUserData.IsDownloaded;
        IsRead = chapterUserData.IsRead;
        LastPageRead = LastPageReadValue(chapterUserData);

        if (_isComicFavorite && !IsDownloaded)
        {
            DownloadItem = _downloadService.GetDownload(_comicKey, Key);
            DownloadItem?.PropertyChanged += OnDownloadItemPropertyChanged;
        }

        NavigateToReaderCommand = navigateToReaderCommand;
        MarkPreviousChaptersAsReadCommand = markPreviousChaptersAsReadCommand;
    }

    public async Task RefreshUserDataAsync()
    {
        var result = await _comicService.GetChapterUserDataAsync(_comicKey, Key);

        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!);
            return;
        }

        var chapterUserData = result.Value!;
        IsDownloaded = chapterUserData.IsDownloaded;
        IsRead = chapterUserData.IsRead;
        LastPageRead = LastPageReadValue(chapterUserData);
    }

    private bool CanToggleDownload() => IsDownloadAvailable;

    [RelayCommand(CanExecute = nameof(CanToggleDownload))]
    private async Task ToggleDownload()
    {
        if (!IsDownloadAvailable)
            return;

        if (IsDownloadQueued || IsDownloading)
        {
            DownloadItem?.Cancel();
        }
        else if (IsDownloaded)
        {
            await _downloadService.DeleteChapterDownloadAsync(_comicKey, Key);
            IsDownloaded = false;
        }
        else
        {
            DownloadItem = null;
            DownloadItem = _downloadService.EnqueueChapterDownload(
                _comicKey,
                Key,
                _comicTitle,
                DisplayTitle!,
                ct => _comicService.GetChapterPagesAsync(_comicKey, Key, forceWeb: true, ct)
            );
            DownloadItem.PropertyChanged += OnDownloadItemPropertyChanged;
        }
    }

    [RelayCommand]
    private async Task ToggleRead()
    {
        var chapterUserData = new ChapterUserData
        {
            IsRead = IsRead,
            LastPageRead = IsRead ? Chapter.Pages : 0,
        };

        var result = await _comicService.UpsertChapterUserDataAsync(
            _comicKey,
            Key,
            chapterUserData
        );

        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!);
            IsRead = !IsRead;
        }

        LastPageRead = LastPageReadValue(chapterUserData);
    }

    private int LastPageReadValue(ChapterUserData chapterUserData) =>
        chapterUserData.IsRead ? Chapter.Pages ?? 0 : chapterUserData.LastPageRead ?? 0;

    private void OnDownloadItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName == nameof(DownloadItem.Status)
            || e.PropertyName == nameof(DownloadItem.Progress)
        )
        {
            if (
                DownloadItem?.Status
                is DownloadStatus.Completed
                    or DownloadStatus.Failed
                    or DownloadStatus.Cancelled
            )
            {
                _ = RefreshUserDataAsync();
                DownloadItem.PropertyChanged -= OnDownloadItemPropertyChanged;
            }

            OnPropertyChanged(nameof(IsDownloadQueued));
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(IsDownloadCancelled));
            OnPropertyChanged(nameof(IsDownloadFailed));
            OnPropertyChanged(nameof(DownloadProgress));
            OnPropertyChanged(nameof(FormattedDownloadProgress));
            OnPropertyChanged(nameof(DownloadButtonValue));
            OnPropertyChanged(nameof(DownloadIcon));
        }
    }
}
