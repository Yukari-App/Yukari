using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Yukari.Models;
using Yukari.Services;

namespace Yukari.ViewModels
{
    public partial class MangaItemViewModel : ObservableObject
    {
        private readonly IMangaService _mangaService;

        private Manga _manga;

        public Guid? Id => _manga?.Id;
        public string Title => _manga?.Title ?? "Loading...";
        public string CoverImageUrl => _manga?.CoverImageUrl;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PinText), nameof(PinIcon))]
        private bool _isPinned;

        public string PinText => IsPinned ? "Unpin" : "Pin";
        public string PinIcon => IsPinned ? "\uE77A" : "\uE718";
        
        public MangaItemViewModel(Manga manga, IMangaService MangaService)
        {
            _manga = manga;
            _mangaService = MangaService;
        }

        [RelayCommand]
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }
    }
}
