using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Yukari.Core.Models.Settings;

namespace Yukari.ViewModels.Components.Settings;

public partial class MultipleChoiceSettingViewModel : SourceSettingViewModel
{
    private readonly HashSet<string> _defaultKeys;

    public ObservableCollection<ToggleOptionViewModel> Options { get; }

    public MultipleChoiceSettingViewModel(MultipleChoiceSetting setting, string? storedValue = null)
        : base(setting)
    {
        var validKeys = setting.Options.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
        _defaultKeys =
            setting.DefaultKeys?.Where(validKeys.Contains).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        HashSet<string>? selectedKeys = null;
        if (!string.IsNullOrEmpty(storedValue))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(storedValue);
                if (parsed != null)
                {
                    selectedKeys = parsed
                        .Where(validKeys.Contains)
                        .ToHashSet(StringComparer.Ordinal);
                }
            }
            catch (JsonException) { }
        }

        selectedKeys ??= _defaultKeys;

        Options = new ObservableCollection<ToggleOptionViewModel>(
            setting.Options.Select(o => new ToggleOptionViewModel(
                o.Key,
                selectedKeys.Contains(o.Key),
                o.DisplayName
            ))
        );
    }

    public override void RestoreDefault()
    {
        foreach (var opt in Options)
            opt.IsSelected = _defaultKeys.Contains(opt.Key);
    }

    public override bool TryGetSaveValue(out string value)
    {
        var selected = Options.Where(o => o.IsSelected).Select(o => o.Key).ToList();
        value = JsonSerializer.Serialize(selected);
        return true;
    }
}
