using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Core.Models.Settings;

namespace Yukari.ViewModels.Components.Settings;

public abstract class SourceSettingViewModel : ObservableObject
{
    public string Key { get; }
    public string Header { get; }
    public string? Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    protected SourceSettingViewModel(SourceSetting setting)
    {
        Key = setting.Key;
        Header = setting.Header;
        Description = setting.Description;
    }

    public abstract void RestoreDefault();
    public abstract bool TryGetSaveValue(out string value);
}
