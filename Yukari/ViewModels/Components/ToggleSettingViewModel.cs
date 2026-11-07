using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Core.Models.Settings;

namespace Yukari.ViewModels.Components;

public partial class ToggleSettingViewModel : SourceSettingViewModel
{
    private readonly bool _defaultValue;

    [ObservableProperty]
    public partial bool Value { get; set; }

    public ToggleSettingViewModel(ToggleSetting setting, string? storedValue = null)
        : base(setting)
    {
        _defaultValue = setting.DefaultValue;
        Value =
            storedValue != null && bool.TryParse(storedValue, out var parsed)
                ? parsed
                : _defaultValue;
    }

    public override void RestoreDefault() => Value = _defaultValue;

    public override bool TryGetSaveValue(out string value)
    {
        value = Value ? "true" : "false";
        return true;
    }
}
