using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Yukari.Core.Models.Settings;
using Yukari.Services.Sources;
using Yukari.Services.UI;
using Yukari.ViewModels.Components.Settings;

namespace Yukari.ViewModels.Dialogs;

public partial class ComicSourceSettingsDialogViewModel : ObservableObject
{
    private readonly ISourceManager _sourceManager;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<ComicSourceSettingsDialogViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DialogTitle))]
    public partial string SourceName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    public partial List<SourceSettingViewModel> Settings { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    public partial bool IsLoading { get; set; }

    public string DialogTitle =>
        _localizationService.GetFormattedString("ComicSourceSettingsHeader", SourceName);

    public bool IsEmpty => !IsLoading && Settings.Count == 0;
    public bool CanInteract => !IsLoading && Settings.Count > 0;

    public ComicSourceSettingsDialogViewModel(
        ISourceManager sourceManager,
        INotificationService notificationService,
        ILocalizationService localizationService,
        ILogger<ComicSourceSettingsDialogViewModel> logger
    )
    {
        _sourceManager = sourceManager;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task InitializeAsync(string sourceName)
    {
        SourceName = sourceName;
        IsLoading = true;

        var result = await _sourceManager.GetSourceSettingsAsync(sourceName);
        if (!result.IsSuccess)
        {
            Settings = [];
            IsLoading = false;
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        var (schema, values) = result.Value;
        var items = new List<SourceSettingViewModel>();

        foreach (var setting in schema)
        {
            values.TryGetValue(setting.Key, out var storedValue);

            SourceSettingViewModel? vm = setting switch
            {
                ToggleSetting toggle => new ToggleSettingViewModel(toggle, storedValue),
                TextSetting text => new TextSettingViewModel(text, storedValue),
                PasswordSetting password => new PasswordSettingViewModel(
                    password,
                    !string.IsNullOrEmpty(storedValue)
                ),
                NumberSetting number => new NumberSettingViewModel(number, storedValue),
                SingleChoiceSetting single => new SingleChoiceSettingViewModel(single, storedValue),
                MultipleChoiceSetting multi => new MultipleChoiceSettingViewModel(
                    multi,
                    storedValue
                ),
                _ => LogAndSkipUnknownSetting(setting),
            };

            if (vm != null)
                items.Add(vm);
        }

        Settings = items;
        IsLoading = false;
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private void RestoreDefaults()
    {
        foreach (var setting in Settings)
            setting.RestoreDefault();
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task SaveAsync()
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var setting in Settings)
        {
            if (setting.TryGetSaveValue(out var value))
                payload[setting.Key] = value;
        }

        var result = await _sourceManager.SaveSettingsAsync(SourceName, payload);
        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        _notificationService.ShowSuccess(
            _localizationService.GetString("SuccessComicSourceSettingsSaved")
        );
    }

    private SourceSettingViewModel? LogAndSkipUnknownSetting(SourceSetting setting)
    {
        _logger.LogWarning(
            "Unknown SourceSetting type '{SettingType}' with key '{Key}' was ignored.",
            setting.GetType().FullName,
            setting.Key
        );
        return null;
    }
}
