using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.ViewModels.Components;

public partial class ComicItemViewModel : ObservableObject
{
    public ComicModel Comic { get; }
    public ContentKey Key => new(Comic.Id, Comic.Source);

    public ComicItemViewModel(ComicModel comic) => Comic = comic;
}
