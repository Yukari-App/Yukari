using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using Yukari.Helpers.UI;
using Yukari.Models;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Components
{
    public partial class ChapterItemViewModel : ObservableObject
    {
        private readonly IComicService _comicService;
        private readonly INotificationService _notificationService;

        private readonly ContentKey _comicKey;
        private readonly bool _isComicFavorite;

        public IRelayCommand<ContentKey>? NavigateToReaderCommand { get; set; }
        public IRelayCommand<ChapterItemViewModel>? MarkPreviousChaptersAsReadCommand { get; set; }

        public ChapterModel Chapter { get; }
        public ContentKey Key => new(Chapter.Id, Chapter.Source);

        public string? DisplayTitle { get; }

        [ObservableProperty] public partial int? LastPageRead { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        public partial bool IsDownloaded { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        public partial bool IsDownloading { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(ReadIcon))] 
        public partial bool IsRead { get; set; }

        public bool IsDownloadAvailable => _isComicFavorite;

        public string DownloadIcon => IsDownloaded ? "\uE74D" : IsDownloading ? "\uF78A" : "\uE896";
        public string ReadIcon => IsRead ? "\uED1A" : "\uE890";

        public ChapterItemViewModel(IComicService comicService, INotificationService notificationService,
                                    ChapterAggregate chapterAggregate, ContentKey comicKey, bool isComicFavorite)
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
            LastPageRead = chapterUserData.LastPageRead ?? (IsRead ? Chapter.Pages : 0);
        }

        [RelayCommand]
        public async Task ToggleRead()
        {
            var result = await _comicService.UpsertChapterUserDataAsync(_comicKey!, Key, new()
            {
                IsDownloaded = IsDownloaded,
                IsRead = IsRead
            });

            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!);
                IsRead = !IsRead;
            }

            LastPageRead = IsRead ? Chapter.Pages : 0;
        }
    }
}
