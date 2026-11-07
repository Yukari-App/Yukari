using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Core.Models.Settings;

namespace Yukari.ViewModels.Components.Settings;

public partial class PasswordSettingViewModel : SourceSettingViewModel
{
    private readonly bool _hadInitialValue;

    public string Placeholder { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    public partial bool HasStoredValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    public partial bool IsCleared { get; set; }

    public bool IsModified => IsCleared || !string.IsNullOrEmpty(Value);

    public PasswordSettingViewModel(PasswordSetting setting, bool hasStoredValue = false)
        : base(setting)
    {
        _hadInitialValue = hasStoredValue;
        HasStoredValue = hasStoredValue;
        Placeholder = setting.Placeholder ?? string.Empty;
    }

    private bool CanClear() => HasStoredValue || !string.IsNullOrEmpty(Value);

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        Value = string.Empty;
        HasStoredValue = false;
        IsCleared = _hadInitialValue;
    }

    public override void RestoreDefault() => Clear();

    public override bool TryGetSaveValue(out string value)
    {
        if (!string.IsNullOrEmpty(Value))
        {
            value = Value;
            return true;
        }

        if (IsCleared)
        {
            value = string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    partial void OnValueChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            IsCleared = false;
    }
}
