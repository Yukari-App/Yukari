using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Helpers.UI;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.ViewModels.Components
{
    public partial class ChapterItemViewModel : ObservableObject
    {
        private bool _isComicFavorite = false;

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

        public ChapterItemViewModel(ChapterAggregate chapterAggregate, bool isComicFavorite)
        {
            Chapter = chapterAggregate.Chapter;
            _isComicFavorite = isComicFavorite;

            DisplayTitle = Chapter.ToDisplayTitle();

            var chapterUserData = chapterAggregate.UserData;
            IsDownloaded = chapterUserData.IsDownloaded;
            IsRead = chapterUserData.IsRead;
            LastPageRead = chapterUserData.LastPageRead ?? (IsRead ? Chapter.Pages : 0);
        }
    }
}
