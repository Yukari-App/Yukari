using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Services.Comics;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Pages
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly IComicService _comicService;
        private readonly IDialogService _dialogService;

        [ObservableProperty] public partial ComicSourceModel? DefaultComicSource { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsComicSourcesEmpty))]
        public partial ObservableCollection<ComicSourceModel> ComicSources { get; set; } = new();

        public bool IsComicSourcesEmpty => ComicSources.Count == 0;

        public SettingsPageViewModel(IComicService comicService, IDialogService dialogService)
        {
            _comicService = comicService;
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
                // TO-DO: Show error notification
                return;
            }

            await LoadComicSourcesAsync();
            // TO-DO: Show success notification
        }

        private async Task LoadComicSourcesAsync()
        {
            ComicSources = new(await _comicService.GetComicSourcesAsync());
        }
    }
}
