using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Yukari.Helpers.UI;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.ViewModels.Components
{
    public partial class ChapterItemViewModel : ObservableObject
    {
        private readonly ChapterModel _chapter;
        private ChapterUserData _chapterUserData;

        private bool _isComicFavorite = false;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        public partial bool IsDownloaded { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        public partial bool IsDownloading { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(ReadIcon))] 
        public partial bool IsRead { get; set; }

        public ContentKey Key => new(_chapter.Id, _chapter.Source);
        public string? DisplayTitle { get; }
        public string ChapterGroups => _chapter.Groups ?? "N/A";
        public DateOnly ChapterLastUpdate => _chapter.LastUpdate;
        public int ChapterPages => _chapter.Pages;
        public int LastPageRead => _chapterUserData.LastPageRead ?? 0;

        public bool IsDownloadAvailable => _isComicFavorite;

        public string DownloadIcon => IsDownloaded ? "\uE74D" : IsDownloading ? "\uF78A" : "\uE896";
        public string ReadIcon => IsRead ? "\uED1A" : "\uE890";

        public ChapterItemViewModel(ChapterAggregate chapterAggregate, bool isComicFavorite)
        {
            _chapter = chapterAggregate.Chapter;
            _chapterUserData = chapterAggregate.UserData;
            _isComicFavorite = isComicFavorite;

            DisplayTitle = _chapter.ToDisplayTitle();
            IsDownloaded = _chapterUserData.IsDownloaded ?? false;
            IsRead = _chapterUserData.IsRead ?? false;
        }

        [RelayCommand]
        public void ToggleDownload()
        {
            IsDownloaded = !IsDownloaded;
        }

        [RelayCommand]
        public void ToggleRead()
        {
            IsRead = !IsRead;
        }
    }
}
