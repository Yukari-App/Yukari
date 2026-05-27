using CommunityToolkit.Mvvm.ComponentModel;

namespace Yukari.ViewModels.Components;

public partial class ToggleOptionViewModel : ObservableObject
{
    public string Key { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public ToggleOptionViewModel(string key, string? displayName = null)
    {
        Key = key;
        DisplayName = displayName ?? key;
    }
}
