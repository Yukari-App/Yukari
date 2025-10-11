using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Yukari.Models;

namespace Yukari.ViewModels
{
    public partial class ChapterItemViewModel : ObservableObject
    {
        private readonly ChapterModel _chapter;

        [ObservableProperty] private string? _displayTitle = "No Title";
        [ObservableProperty] private string _chapterGroups = "N/A";
        [ObservableProperty] private DateOnly _chapterLastUpdate = DateOnly.MinValue;
        [ObservableProperty] private int _chapterPages = 0;
        [ObservableProperty] private int _lastPageRead = 0;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        private bool _isDownloaded;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        private bool _isDownloading;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(ReadIcon))] 
        private bool _isRead;

        public ContentIdentifier Identifier => new(_chapter.Id, _chapter.Source);
        public string DownloadIcon => IsDownloaded ? "\uE74D" : IsDownloading ? "\uF78A" : "\uE896";
        public string ReadIcon => IsRead ? "\uED1A" : "\uE890";

        public ChapterItemViewModel(ChapterModel chapter)
        {
            _chapter = chapter;

            DisplayTitle = (chapter?.Volume != null ? $"[{chapter.Volume}] " : string.Empty) +
                (chapter?.Number != null ? $"#{chapter.Number} " : "#N/A ") +
                (chapter?.Title ?? string.Empty);

            ChapterGroups = chapter?.Groups ?? "N/A";
            ChapterLastUpdate = chapter?.LastUpdate ?? DateOnly.FromDateTime(DateTime.MinValue);
            ChapterPages = chapter?.Pages ?? 0;
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
