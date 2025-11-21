using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.ViewModels.Components
{
    public partial class ChapterItemViewModel : ObservableObject
    {
        private readonly ChapterModel _chapter;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        private bool _isDownloaded;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(DownloadIcon))]
        private bool _isDownloading;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(ReadIcon))] 
        private bool _isRead;

        public string DownloadIcon => IsDownloaded ? "\uE74D" : IsDownloading ? "\uF78A" : "\uE896";
        public string ReadIcon => IsRead ? "\uED1A" : "\uE890";

        public ContentKey Key => new(_chapter.Id, _chapter.Source);
        public string? DisplayTitle { get; }
        public string ChapterGroups => _chapter.Groups ?? "N/A";
        public DateOnly ChapterLastUpdate => _chapter.LastUpdate;
        public int ChapterPages => _chapter.Pages;
        public int LastPageRead => _chapter.LastPageRead ?? 0;

        public ChapterItemViewModel(ChapterModel chapter)
        {
            _chapter = chapter;

            DisplayTitle = FormatDisplayTitle();
            IsDownloaded = chapter.IsDownloaded ?? false;
            IsRead = chapter.IsRead ?? false;
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

        private string FormatDisplayTitle()
        {
            var volume = _chapter.Volume != null ? $"[{_chapter.Volume}] " : string.Empty;
            var number = _chapter.Number != null ? $"#{_chapter.Number} " : "#N/A ";
            var title = _chapter.Title ?? string.Empty;

            return $"{volume}{number}{title}";
        }
    }
}
