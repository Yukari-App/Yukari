using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.ViewModels.Components
{
    public partial class ComicItemViewModel : ObservableObject
    {
        public ComicModel Comic { get; }
        public ContentKey Key => new(Comic.Id, Comic.Source);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PinText), nameof(PinIcon))]
        public partial bool IsPinned { get; set; }

        public string PinText => IsPinned ? "Unpin" : "Pin";
        public string PinIcon => IsPinned ? "\uE77A" : "\uE718";

        public ComicItemViewModel(ComicModel comic) =>
            Comic = comic;

        [RelayCommand]
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }
    }
}
