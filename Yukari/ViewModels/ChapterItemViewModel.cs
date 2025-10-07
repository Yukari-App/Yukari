using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Yukari.Models;

namespace Yukari.ViewModels
{
    internal partial class ChapterItemViewModel : ObservableObject
    {
        private ChapterModel _chapter;

        public string ChapterId => _chapter.Id;
        public string? ChapterTitle => _chapter.Title;
        public int ChapterNumber => _chapter.Number;
        public string ChapterVolume => _chapter.Volume;
        public string ChapterGroups => _chapter.Groups;
        public DateOnly ChapterLastUpdate => _chapter.LastUpdate;
        public int ChapterPages => _chapter.Pages;

        [ObservableProperty]
        private int _lastPageRead;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadIcon))]
        private bool _isDownloaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadIcon))]
        private bool _isDownloading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ReadIcon))] 
        private bool _isRead;

        public string DownloadIcon => IsDownloaded ? "\uE74D" : IsDownloading ? "\uF78A" : "\uE896";
        public string ReadIcon => IsRead ? "\uED1A" : "\uE890";

        public ChapterItemViewModel(ChapterModel chapter)
        {
            _chapter = chapter;

            LastPageRead = chapter?.LastPageRead ?? 0;

            IsDownloaded = chapter?.IsDownloaded ?? false;
            IsRead = chapter?.IsRead ?? false;
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
