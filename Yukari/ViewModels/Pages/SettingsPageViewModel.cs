using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Pages
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IComicService _comicService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;

        public string YukariVersion { get; } = AppInfoHelper.Version;

        public ThemeMode[] AvailableThemeModes { get; } = Enum.GetValues<ThemeMode>();

        [ObservableProperty]
        public partial ThemeMode SelectedThemeMode { get; set; }

        [ObservableProperty]
        public partial ComicSourceModel? DefaultComicSource { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsComicSourcesEmpty))]
        public partial List<ComicSourceModel> ComicSources { get; set; } = new();

        public bool IsComicSourcesEmpty => ComicSources.Count == 0;

        public SettingsPageViewModel(
            ISettingsService settingsService,
            IComicService comicService,
            INotificationService notificationService,
            IDialogService dialogService
        )
        {
            _settingsService = settingsService;
            _comicService = comicService;
            _notificationService = notificationService;
            _dialogService = dialogService;

            _ = LoadSettingsAsync();

            _notificationService.ShowWarning(
                "Settings are not currently persisted, except adding ComicSources and changing theme"
            );
        }

        public async Task OnNavigatedFromAsync() => await _settingsService.SaveAsync();

        private async Task LoadSettingsAsync()
        {
            SelectedThemeMode = _settingsService.Current.Theme;

            await LoadComicSourcesAsync();
            if (ComicSources.Count > 0)
            {
                DefaultComicSource =
                    ComicSources.FirstOrDefault(s =>
                        s.Name == _settingsService.Current.DefaultComicSourceName
                    ) ?? ComicSources.First();
            }
        }

        [RelayCommand]
        private async Task AddComicSourceAsync()
        {
            var pluginPath = await _dialogService.OpenFilePickerAsync(".dll");
            if (pluginPath == null)
                return;

            var result = await _comicService.UpsertComicSourceAsync(pluginPath);
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!);
                return;
            }

            await LoadComicSourcesAsync();
            DefaultComicSource ??=
                ComicSources.FirstOrDefault(s =>
                    s.Name == _settingsService.Current.DefaultComicSourceName
                ) ?? ComicSources.FirstOrDefault();

            _notificationService.ShowSuccess("Comic source added successfully.");
        }

        private async Task LoadComicSourcesAsync()
        {
            var result = await _comicService.GetComicSourcesAsync();
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!);
                return;
            }

            ComicSources = result.Value!.ToList();
        }

        partial void OnSelectedThemeModeChanged(ThemeMode value) =>
            _settingsService.Set(s => s.Theme, value);

        partial void OnDefaultComicSourceChanged(ComicSourceModel? value) =>
            _settingsService.Set(s => s.DefaultComicSourceName, value?.Name);
    }
}
