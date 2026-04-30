using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Helpers.UI;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Components;

public partial class ChapterItemViewModel : ObservableObject
{
    private readonly IComicService _comicService;
    private readonly INotificationService _notificationService;

    private readonly ContentKey _comicKey;
    private readonly bool _isComicFavorite;

    public IRelayCommand<ContentKey>? NavigateToReaderCommand { get; }
    public IRelayCommand<ChapterItemViewModel>? MarkPreviousChaptersAsReadCommand { get; }

    public ChapterModel Chapter { get; }
    public ContentKey Key => new(Chapter.Id, Chapter.Source);

    public string? DisplayTitle { get; }

    [ObservableProperty]
    public partial int? LastPageRead { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(DownloadIcon),
        nameof(IsChapterAvailable),
        nameof(IsDownloadAvailable)
    )]
    public partial bool IsDownloaded { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ReadIcon))]
    public partial bool IsRead { get; set; }

    public bool IsChapterAvailable => Chapter.IsAvailable || IsDownloaded;
    public bool IsDownloadAvailable => _isComicFavorite && IsChapterAvailable;

    public string DownloadIcon =>
        IsDownloaded ? "\uE74D"
        : IsDownloading ? "\uF78A"
        : "\uE896";
    public string ReadIcon => IsRead ? "\uED1A" : "\uE890";

    public ChapterItemViewModel(
        IComicService comicService,
        INotificationService notificationService,
        ChapterAggregate chapterAggregate,
        ContentKey comicKey,
        bool isComicFavorite,
        IRelayCommand<ContentKey> navigateToReaderCommand,
        IRelayCommand<ChapterItemViewModel> markPreviousChaptersAsReadCommand
    )
    {
        _comicService = comicService;
        _notificationService = notificationService;

        _comicKey = comicKey;
        Chapter = chapterAggregate.Chapter;
        _isComicFavorite = isComicFavorite;

        DisplayTitle = Chapter.ToDisplayTitle();

        var chapterUserData = chapterAggregate.UserData;
        IsDownloaded = chapterUserData.IsDownloaded;
        IsRead = chapterUserData.IsRead;
        LastPageRead = LastPageReadValue(chapterUserData);

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
    private void ToggleDownload()
    {
        _notificationService.ShowWarning("Download chapters feature is not implemented yet.");
        IsDownloaded = !IsDownloaded;
    }

    [RelayCommand]
    private async Task ToggleRead()
    {
        var chapterUserData = new ChapterUserData
        {
            IsDownloaded = IsDownloaded,
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
        chapterUserData.IsRead ? Chapter.Pages : chapterUserData.LastPageRead ?? 0;
}
