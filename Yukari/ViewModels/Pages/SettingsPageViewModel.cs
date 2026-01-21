using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Services.Comics;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Pages
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;

        [ObservableProperty] public partial ComicSourceModel? DefaultComicSource { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsComicSourcesEmpty))]
        public partial List<ComicSourceModel> ComicSources { get; set; } = new();

        public bool IsComicSourcesEmpty => ComicSources.Count == 0;

        public SettingsPageViewModel(IComicService comicService, INotificationService notificationService, IDialogService dialogService)
        {
            _comicService = comicService;
            _notificationService = notificationService;
            _dialogService = dialogService;

            _ = LoadComicSourcesAsync();
        }

        [RelayCommand]
        public async Task AddComicSourceAsync()
        {
            var pluginPath = await _dialogService.OpenFilePickerAsync(".dll");
            if (pluginPath == null) return;

            var result = await _comicService.UpsertComicSourceAsync(pluginPath);
            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!);
                return;
            }

            await LoadComicSourcesAsync();
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
    }
}
