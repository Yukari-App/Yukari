using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Models;
using Yukari.Services.Comics;

namespace Yukari.ViewModels.Components
{
    public partial class ComicItemViewModel : ObservableObject
    {
        private readonly IComicService _comicService;

        private ComicModel _comic;

        public ContentIdentifier? Identifier => new(_comic?.Id, _comic.Source);
        public string Title => _comic?.Title ?? "Unknown Name";
        public string CoverImageUrl => _comic?.CoverImageUrl;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PinText), nameof(PinIcon))]
        private bool _isPinned;

        public string PinText => IsPinned ? "Unpin" : "Pin";
        public string PinIcon => IsPinned ? "\uE77A" : "\uE718";
        
        public ComicItemViewModel(ComicModel comic, IComicService comicService)
        {
            _comic = comic;
            _comicService = comicService;
        }

        [RelayCommand]
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }
    }
}
