using System;
using System.Collections.ObjectModel;
using System.Linq;
using Yukari.Core.Models.Settings;

namespace Yukari.ViewModels.Components.Settings;

public partial class SingleChoiceSettingViewModel : SourceSettingViewModel
{
    private readonly string _defaultKey;

    public ObservableCollection<ToggleOptionViewModel> Options { get; }

    // OnPropertyChanged is intentionally omitted in the setter to avoid a TwoWay ComboBox loop.
    public ToggleOptionViewModel? SelectedOption
    {
        get => Options.FirstOrDefault(o => o.IsSelected);
        set
        {
            if (value is null)
                return;

            foreach (var opt in Options)
                opt.IsSelected = false;

            value.IsSelected = true;
        }
    }

    public SingleChoiceSettingViewModel(SingleChoiceSetting setting, string? storedValue = null)
        : base(setting)
    {
        var validKeys = setting.Options.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
        _defaultKey =
            setting.DefaultKey != null && validKeys.Contains(setting.DefaultKey)
                ? setting.DefaultKey
                : setting.Options.FirstOrDefault()?.Key ?? string.Empty;

        var initialKey =
            storedValue != null && validKeys.Contains(storedValue) ? storedValue : _defaultKey;

        Options = new ObservableCollection<ToggleOptionViewModel>(
            setting.Options.Select(o => new ToggleOptionViewModel(
                o.Key,
                o.Key == initialKey,
                o.DisplayName
            ))
        );
    }

    public override void RestoreDefault()
    {
        foreach (var opt in Options)
            opt.IsSelected = opt.Key == _defaultKey;

        OnPropertyChanged(nameof(SelectedOption));
    }

    public override bool TryGetSaveValue(out string value)
    {
        value = SelectedOption?.Key ?? _defaultKey;
        return true;
    }
}
