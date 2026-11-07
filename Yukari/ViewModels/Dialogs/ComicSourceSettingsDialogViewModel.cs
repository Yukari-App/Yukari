using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Yukari.Core.Models.Settings;
using Yukari.Services.Security;
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
    private readonly ICredentialProtector? _credentialProtector;
    private readonly ISourceService? _sourceService;

    [ObservableProperty]
    public partial string SourceName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyPropertyChangedFor(nameof(IsCredentialsLocked))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    public partial List<SourceSettingViewModel> Settings { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string UnlockPassword { get; set; } = string.Empty;

    public bool IsEmpty => !IsLoading && Settings.Count == 0;
    public bool CanInteract => !IsLoading && Settings.Count > 0;
    public bool IsCredentialsLocked =>
        (_credentialProtector?.UsesEntropy ?? false)
        && !(_credentialProtector?.IsUnlocked ?? true)
        && Settings.OfType<PasswordSettingViewModel>().Any();

    public ComicSourceSettingsDialogViewModel(
        ISourceManager sourceManager,
        INotificationService notificationService,
        ILocalizationService localizationService,
        ILogger<ComicSourceSettingsDialogViewModel> logger,
        ICredentialProtector? credentialProtector = null,
        ISourceService? sourceService = null
    )
    {
        _sourceManager = sourceManager;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _logger = logger;
        _credentialProtector = credentialProtector;
        _sourceService = sourceService;
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
        var isLocked =
            (_credentialProtector?.UsesEntropy ?? false)
            && !(_credentialProtector?.IsUnlocked ?? true);

        foreach (var setting in schema)
        {
            values.TryGetValue(setting.Key, out var storedValue);

            SourceSettingViewModel? vm = setting switch
            {
                ToggleSetting toggle => new ToggleSettingViewModel(toggle, storedValue),
                TextSetting text => new TextSettingViewModel(text, storedValue),
                PasswordSetting password => new PasswordSettingViewModel(
                    password,
                    !string.IsNullOrEmpty(storedValue),
                    isLocked
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
        OnPropertyChanged(nameof(IsCredentialsLocked));
    }

    [RelayCommand]
    private async Task UnlockCredentialsAsync()
    {
        if (_credentialProtector == null)
            return;

        if (!_credentialProtector.TryUnlock(UnlockPassword))
        {
            _notificationService.ShowError(
                _localizationService.GetString("ErrorIncorrectCredentialPassword")
            );
            return;
        }

        UnlockPassword = string.Empty;
        if (_sourceService != null)
            await _sourceService.ApplySettingsToAllLoadedSourcesAsync();

        _notificationService.ShowSuccess(
            _localizationService.GetString("SuccessCredentialsUnlocked")
        );
        await InitializeAsync(SourceName);
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
