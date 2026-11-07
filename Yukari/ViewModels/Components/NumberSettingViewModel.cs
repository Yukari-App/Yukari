using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Core.Models.Settings;

namespace Yukari.ViewModels.Components;

public partial class NumberSettingViewModel : SourceSettingViewModel
{
    private readonly double _defaultValue;
    private readonly double? _minimum;
    private readonly double? _maximum;

    public string Placeholder { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Step { get; }

    [ObservableProperty]
    public partial double Value { get; set; }

    public NumberSettingViewModel(NumberSetting setting, string? storedValue = null)
        : base(setting)
    {
        _minimum = setting.Minimum;
        _maximum = setting.Maximum;
        _defaultValue = Clamp(setting.DefaultValue, _minimum, _maximum);

        Placeholder = setting.Placeholder ?? string.Empty;
        Minimum = setting.Minimum ?? double.MinValue;
        Maximum = setting.Maximum ?? double.MaxValue;
        Step = setting.Step is > 0 ? setting.Step.Value : 1.0;

        if (
            storedValue != null
            && double.TryParse(
                storedValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed
            )
        )
        {
            Value = Clamp(parsed, _minimum, _maximum);
        }
        else
        {
            Value = _defaultValue;
        }
    }

    public override void RestoreDefault() => Value = _defaultValue;

    public override bool TryGetSaveValue(out string value)
    {
        var cleanValue = double.IsNaN(Value) ? _defaultValue : Clamp(Value, _minimum, _maximum);
        value = cleanValue.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static double Clamp(double val, double? min, double? max)
    {
        if (min.HasValue && val < min.Value)
            return min.Value;
        if (max.HasValue && val > max.Value)
            return max.Value;
        return val;
    }
}
