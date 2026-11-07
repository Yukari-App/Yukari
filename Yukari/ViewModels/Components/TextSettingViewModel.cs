using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Core.Models.Settings;

namespace Yukari.ViewModels.Components;

public partial class TextSettingViewModel : SourceSettingViewModel
{
    private readonly string _defaultValue;

    public string Placeholder { get; }

    [ObservableProperty]
    public partial string Value { get; set; }

    public TextSettingViewModel(TextSetting setting, string? storedValue = null)
        : base(setting)
    {
        _defaultValue = setting.DefaultValue ?? string.Empty;
        Placeholder = setting.Placeholder ?? string.Empty;
        Value = storedValue ?? _defaultValue;
    }

    public override void RestoreDefault() => Value = _defaultValue;

    public override bool TryGetSaveValue(out string value)
    {
        value = Value ?? string.Empty;
        return true;
    }
}
