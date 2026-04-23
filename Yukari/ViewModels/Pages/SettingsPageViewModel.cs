using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Settings;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IComicService _comicService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;
        private readonly IMessenger _messenger;

        private bool _isInitializing = true;

        public string YukariVersion { get; } = AppInfoHelper.Version;
        public string CoreVersion { get; } = AppInfoHelper.CoreVersion;

        public ThemeMode[] AvailableThemeModes { get; } = Enum.GetValues<ThemeMode>();
        public ReadingMode[] AvailableReadingModes { get; } = Enum.GetValues<ReadingMode>();
        public ScalingMode[] AvailableScalingModes { get; } = Enum.GetValues<ScalingMode>();

        [ObservableProperty]
        public partial ThemeMode SelectedThemeMode { get; set; }

        [ObservableProperty]
        public partial bool IsAutoFullscreen { get; set; }

        [ObservableProperty]
        public partial ReadingMode SelectedReadingMode { get; set; }

        [ObservableProperty]
        public partial ScalingMode SelectedScalingMode { get; set; }

        [ObservableProperty]
        public partial ComicSourceModel? DefaultComicSource { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyComicSourceEnabled))]
        public partial List<ComicSourceModel> AvailableComicSources { get; set; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsComicSourcesEmpty))]
        public partial List<ComicSourceItemViewModel> ComicSourceItems { get; set; } = new();

        public bool IsAnyComicSourceEnabled => AvailableComicSources.Count > 0;
        public bool IsComicSourcesEmpty => ComicSourceItems.Count == 0;

        public SettingsPageViewModel(
            ISettingsService settingsService,
            IComicService comicService,
            INotificationService notificationService,
            IDialogService dialogService,
            IMessenger messenger
        )
        {
            _settingsService = settingsService;
            _comicService = comicService;
            _notificationService = notificationService;
            _dialogService = dialogService;
            _messenger = messenger;

            _ = LoadSettingsAsync();
        }

        public async Task OnNavigatedFromAsync() => await _settingsService.SaveAsync();

        private async Task LoadSettingsAsync()
        {
            SelectedThemeMode = _settingsService.Current.Theme;
            IsAutoFullscreen = _settingsService.Current.AutoFullscreen;
            SelectedReadingMode = _settingsService.Current.ReadingMode;
            SelectedScalingMode = _settingsService.Current.ScalingMode;

            await LoadComicSourcesAsync();
            if (AvailableComicSources.Count > 0)
            {
                DefaultComicSource =
                    AvailableComicSources.FirstOrDefault(s =>
                        s.Name == _settingsService.Current.DefaultComicSourceName
                    ) ?? AvailableComicSources.First();
            }

            _isInitializing = false;
        }

        [RelayCommand]
        private async Task AddComicSourceAsync()
        {
            var pluginPath = await _dialogService.OpenFilePickerAsync(".dll");
            if (pluginPath == null)
                return;

            var result = await _comicService.UpsertComicSourceAsync(pluginPath);
            if (result.Kind == ResultKind.PendingRestart)
            {
                _notificationService.ShowWarning(
                    "The source is already in use and cannot be updated now. Restart Yukari to complete the update."
                );
                return;
            }
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!, result.ErrorTitle!);
                return;
            }

            await LoadComicSourcesAsync();

            DefaultComicSource =
                AvailableComicSources.FirstOrDefault(s =>
                    s.Name == _settingsService.Current.DefaultComicSourceName
                ) ?? AvailableComicSources.First();

            _notificationService.ShowSuccess("Comic source added successfully.");
            _messenger.Send(new ComicSourcesUpdatedMessage());
        }

        [RelayCommand]
        private async Task RemoveComicSourceAsync(ComicSourceItemViewModel comicSourceItem)
        {
            var result = await _comicService.RemoveComicSourceAsync(
                comicSourceItem.ComicSource.Name
            );

            if (result.Kind == ResultKind.PendingRestart)
            {
                _notificationService.ShowWarning(
                    "The source is currently in use and cannot be removed. Restart Yukari to complete the removal."
                );
                return;
            }
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!, result.ErrorTitle!);
                return;
            }

            await LoadComicSourcesAsync();
            DefaultComicSource =
                AvailableComicSources.FirstOrDefault(s =>
                    s.Name == _settingsService.Current.DefaultComicSourceName
                ) ?? AvailableComicSources.FirstOrDefault();

            _messenger.Send(new ComicSourcesUpdatedMessage());
        }

        [RelayCommand]
        private async Task OnComicSourceIsEnabledChanged((string Name, bool IsEnabled) args)
        {
            var result = await _comicService.UpdateComicSourceIsEnabledAsync(
                args.Name,
                args.IsEnabled
            );
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!, result.ErrorTitle!);
                return;
            }

            AvailableComicSources = ComicSourceItems
                .Where(cs => cs.IsEnabled)
                .Select(cs => cs.ComicSource)
                .ToList();

            DefaultComicSource =
                AvailableComicSources.FirstOrDefault(s =>
                    s.Name == _settingsService.Current.DefaultComicSourceName
                ) ?? AvailableComicSources.FirstOrDefault();

            _messenger.Send(new ComicSourcesUpdatedMessage());
        }

        [RelayCommand]
        private async Task CleanUpStorageAsync()
        {
            var result = await _comicService.CleanupUnfavoriteComicsDataAsync();
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!, result.ErrorTitle!);
                return;
            }

            _notificationService.ShowSuccess("Storage cleaned up successfully.");
        }

        private async Task LoadComicSourcesAsync()
        {
            var result = await _comicService.GetComicSourcesAsync();
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!, result.ErrorTitle!);
                return;
            }

            var comicSources = result.Value!;

            AvailableComicSources = comicSources.Where(cs => cs.IsEnabled).ToList();
            ComicSourceItems = comicSources
                .Select(cs => new ComicSourceItemViewModel(
                    cs,
                    RemoveComicSourceCommand,
                    ComicSourceIsEnabledChangedCommand
                ))
                .ToList();
        }

        private void ApplySetting<T>(Expression<Func<AppSettings, T>> selector, T value)
        {
            if (_isInitializing)
                return;
            _settingsService.Set(selector, value);
        }

        partial void OnSelectedThemeModeChanged(ThemeMode value) =>
            ApplySetting(s => s.Theme, value);

        partial void OnIsAutoFullscreenChanged(bool value) =>
            ApplySetting(s => s.AutoFullscreen, value);

        partial void OnSelectedReadingModeChanged(ReadingMode value) =>
            ApplySetting(s => s.ReadingMode, value);

        partial void OnSelectedScalingModeChanged(ScalingMode value) =>
            ApplySetting(s => s.ScalingMode, value);

        partial void OnDefaultComicSourceChanged(ComicSourceModel? value)
        {
            if (value == null)
                return;
            ApplySetting(s => s.DefaultComicSourceName, value?.Name);
        }
    }
}
