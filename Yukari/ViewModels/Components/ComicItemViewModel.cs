using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.ViewModels.Components
{
    public partial class ComicItemViewModel : ObservableObject
    {
        private readonly ComicModel _comic;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PinText), nameof(PinIcon))]
        private bool _isPinned;

        public string PinText => IsPinned ? "Unpin" : "Pin";
        public string PinIcon => IsPinned ? "\uE77A" : "\uE718";
        
        public ContentKey? Key => new(_comic.Id, _comic.Source);
        public string Title => _comic.Title;
        public string? CoverImageUrl => _comic.CoverImageUrl;

        public ComicItemViewModel(ComicModel comic) =>
            _comic = comic;

        [RelayCommand]
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }
    }
}
