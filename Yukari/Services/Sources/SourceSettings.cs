using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yukari.Core.Models.Settings;
using Yukari.Core.Sources;

namespace Yukari.Services.Sources;

internal class SourceSettings : ISourceSettings
{
    private readonly Dictionary<string, bool> _toggles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _numbers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _stringLists = new(
        StringComparer.Ordinal
    );

    public SourceSettings(
        IReadOnlyList<SourceSetting> schema,
        IReadOnlyDictionary<string, string>? storedValues,
        ILogger? logger = null
    )
    {
        storedValues ??= new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var setting in schema)
        {
            switch (setting)
            {
                case ToggleSetting toggle:
                    if (
                        storedValues.TryGetValue(toggle.Key, out var toggleStr)
                        && bool.TryParse(toggleStr, out var parsedBool)
                    )
                    {
                        _toggles[toggle.Key] = parsedBool;
                    }
                    else
                    {
                        _toggles[toggle.Key] = toggle.DefaultValue;
                    }
                    break;

                case TextSetting text:
                    if (storedValues.TryGetValue(text.Key, out var textStr))
                    {
                        _strings[text.Key] = textStr;
                    }
                    else
                    {
                        _strings[text.Key] = text.DefaultValue ?? string.Empty;
                    }
                    break;

                case PasswordSetting password:
                    if (storedValues.TryGetValue(password.Key, out var passwordStr))
                    {
                        _strings[password.Key] = passwordStr;
                    }
                    else
                    {
                        _strings[password.Key] = string.Empty;
                    }
                    break;

                case NumberSetting number:
                    double numVal = number.DefaultValue;
                    if (
                        storedValues.TryGetValue(number.Key, out var numStr)
                        && double.TryParse(
                            numStr,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var parsedDouble
                        )
                    )
                    {
                        numVal = parsedDouble;
                    }

                    if (number.Minimum.HasValue && numVal < number.Minimum.Value)
                    {
                        numVal = number.Minimum.Value;
                    }
                    if (number.Maximum.HasValue && numVal > number.Maximum.Value)
                    {
                        numVal = number.Maximum.Value;
                    }

                    _numbers[number.Key] = numVal;
                    break;

                case SingleChoiceSetting singleChoice:
                    var validSingleKeys = singleChoice
                        .Options.Select(o => o.Key)
                        .ToHashSet(StringComparer.Ordinal);
                    var singleDefault =
                        singleChoice.DefaultKey
                        ?? singleChoice.Options.FirstOrDefault()?.Key
                        ?? string.Empty;

                    if (
                        storedValues.TryGetValue(singleChoice.Key, out var choiceStr)
                        && validSingleKeys.Contains(choiceStr)
                    )
                    {
                        _strings[singleChoice.Key] = choiceStr;
                    }
                    else
                    {
                        _strings[singleChoice.Key] = singleDefault;
                    }
                    break;

                case MultipleChoiceSetting multipleChoice:
                    var validMultipleKeys = multipleChoice
                        .Options.Select(o => o.Key)
                        .ToHashSet(StringComparer.Ordinal);
                    var multiDefault =
                        multipleChoice
                            .DefaultKeys?.Where(k => validMultipleKeys.Contains(k))
                            .ToList()
                        ?? (IReadOnlyList<string>)[];

                    if (storedValues.TryGetValue(multipleChoice.Key, out var jsonStr))
                    {
                        try
                        {
                            var parsedList = JsonSerializer.Deserialize<List<string>>(jsonStr);
                            if (parsedList != null)
                            {
                                _stringLists[multipleChoice.Key] = parsedList
                                    .Where(k => validMultipleKeys.Contains(k))
                                    .ToList();
                            }
                            else
                            {
                                _stringLists[multipleChoice.Key] = multiDefault;
                            }
                        }
                        catch (JsonException)
                        {
                            _stringLists[multipleChoice.Key] = multiDefault;
                        }
                    }
                    else
                    {
                        _stringLists[multipleChoice.Key] = multiDefault;
                    }
                    break;

                default:
                    logger?.LogWarning(
                        "Unknown SourceSetting type '{SettingType}' with key '{Key}' was ignored.",
                        setting.GetType().FullName,
                        setting.Key
                    );
                    break;
            }
        }
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        return _toggles.TryGetValue(key, out var val) ? val : defaultValue;
    }

    public string GetString(string key, string defaultValue = "")
    {
        return _strings.TryGetValue(key, out var val) ? val : defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return _numbers.TryGetValue(key, out var val) ? (int)Math.Round(val) : defaultValue;
    }

    public double GetDouble(string key, double defaultValue = 0.0)
    {
        return _numbers.TryGetValue(key, out var val) ? val : defaultValue;
    }

    public IReadOnlyList<string> GetStringList(
        string key,
        IReadOnlyList<string>? defaultValue = null
    )
    {
        return _stringLists.TryGetValue(key, out var val) ? val : defaultValue ?? [];
    }
}
