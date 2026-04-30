using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Yukari.Helpers;
using Yukari.Models.DTO;
using Yukari.Models.Settings;

namespace Yukari.Services.Settings;

internal class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppSettings _current = new();

    public AppSettings Current => _current;
    public event EventHandler<SettingsChangedEventArgs>? SettingChanged;

    public SettingsService()
    {
        _filePath = Path.Combine(AppDataHelper.GetAppDataPath(), "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            _current = new AppSettings();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _current =
                JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            // TO-DO: Log error
            _current = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_current, _jsonOptions);

        var tmpPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json);
        File.Move(tmpPath, _filePath, overwrite: true);
    }

    public void Set<T>(Expression<Func<AppSettings, T>> selector, T value)
    {
        Expression body = selector.Body;
        if (body is UnaryExpression unary)
            body = unary.Operand;

        if (body is not MemberExpression memberExpr)
            throw new ArgumentException(
                "Selector must be a property access expression.",
                nameof(selector)
            );

        if (memberExpr.Member is not PropertyInfo propInfo)
            throw new ArgumentException("Selector must target a property.", nameof(selector));

        var oldValue = propInfo.GetValue(_current);
        propInfo.SetValue(_current, value);

        SettingChanged?.Invoke(
            this,
            new SettingsChangedEventArgs
            {
                PropertyName = propInfo.Name,
                OldValue = oldValue,
                NewValue = value,
            }
        );
    }

    public void Reset() => _current = new AppSettings();
}
