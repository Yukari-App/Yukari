using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yukari.ViewModels.Components;

public partial class ToggleOptionViewModel : ObservableObject
{
    private bool _isInitializing = true;

    public string Key { get; }
    public string DisplayName { get; }

    public IRelayCommand<ToggleOptionViewModel>? ToggleCommand { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public ToggleOptionViewModel(
        string key,
        bool isSelected,
        string? displayName = null,
        IRelayCommand<ToggleOptionViewModel>? toggleCommand = null
    )
    {
        Key = key;
        IsSelected = isSelected;
        DisplayName = displayName ?? key;
        ToggleCommand = toggleCommand;

        _isInitializing = false;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_isInitializing)
            ToggleCommand?.Execute(this);
    }
}
