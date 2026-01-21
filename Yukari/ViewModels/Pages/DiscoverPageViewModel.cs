using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class DiscoverPageViewModel : ObservableObject,
        IRecipient<SearchChangedMessage>, IRecipient<ComicSourcesUpdatedMessage>
    {
        private readonly IComicService _comicService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly IMessenger _messenger;

        private bool _isDirty = false;
        private bool _isIsActive = false;

        private IReadOnlyList<Filter>? _availableFilters;
        private IReadOnlyDictionary<string, IReadOnlyList<string>>? _appliedFilters;

        private string _searchText = string.Empty;

        [ObservableProperty] public partial List<ComicSourceModel>? ComicSources { get; set; }
        [ObservableProperty] public partial List<ComicItemViewModel>? SearchedComics { get; set; }
        
        [ObservableProperty] public partial ComicSourceModel? SelectedComicSource { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(NoResults)), NotifyCanExecuteChangedFor(nameof(FilterCommand))]
        public partial bool IsContentLoading { get; set; } = true;

        public bool NoResults => !IsContentLoading && (SearchedComics == null || SearchedComics.Count == 0);

        public DiscoverPageViewModel(
            IComicService comicService, IDialogService dialogService, INotificationService notificationService, IMessenger messenger)
        {
            _comicService = comicService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _messenger = messenger;

            _messenger.Register<ComicSourcesUpdatedMessage>(this);
        }

        public void Receive(SearchChangedMessage message)
        {
            _searchText = message.SearchText ?? string.Empty;
            _ = UpdateDisplayedComicsAsync();
        }

        public void Receive(ComicSourcesUpdatedMessage message)
        {
            if (_isIsActive)
                _ = UpdateAvailableComicSources();
            else
                _isDirty = true;
        }

        public void OnNavigatedTo()
        {
            _isIsActive = true;
            _messenger.Register<SearchChangedMessage>(this);

            _messenger.Send(new SetSearchTextMessage(_searchText));

            if (_isDirty || ComicSources == null || ComicSources.Count == 0)
            {
                _ = UpdateAvailableComicSources();
                _isDirty = false;
            }
        }

        public void OnNavigatedFrom()
        {
            _messenger.Unregister<SearchChangedMessage>(this);
            _isIsActive = false;
        }

        private bool CanFilter() => _availableFilters != null && _availableFilters.Count > 0 && !IsContentLoading;

        [RelayCommand(CanExecute = nameof(CanFilter))]
        private async Task OnFilter()
        {
            if (_availableFilters == null) return;

            var newAppliedFilters = await _dialogService.ShowFiltersDialogAsync(
                _availableFilters,
                _appliedFilters ?? new Dictionary<string, IReadOnlyList<string>>());

            if (newAppliedFilters == null) return;

            _appliedFilters = newAppliedFilters;
            await UpdateDisplayedComicsAsync();
        }

        [RelayCommand]
        private void NavigateToComic(ContentKey ComicKey) =>
            _messenger.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), ComicKey));

        private async Task UpdateAvailableComicSources()
        {
            var result = await _comicService.GetComicSourcesAsync();
            if (result.IsSuccess)
            {
                ComicSources = result.Value?.Where(s => s.IsEnabled).ToList();

                if (SelectedComicSource == null || !ComicSources!.Any(x => x.Name == SelectedComicSource.Name))
                {
                    SelectedComicSource = ComicSources?.FirstOrDefault();
                    // Note: Setting SelectedComicSource triggers OnSelectedComicSourceChanged automatically
                }
            }
            else
            {
                _notificationService.ShowError(result.Error!);
            }
        }

        private async Task UpdateAvailableFiltersAsync()
        {
            if (SelectedComicSource == null) return;
            _appliedFilters = null;

            var result = await _comicService.GetSourceFiltersAsync(SelectedComicSource.Name);

            if (!result.IsSuccess)
            {
                _notificationService.ShowError(result.Error!);
                return;
            }

            _availableFilters = result.Value;
            FilterCommand.NotifyCanExecuteChanged();
        }

        private async Task UpdateDisplayedComicsAsync()
        {
            if (SelectedComicSource == null) return;
            IsContentLoading = true;

            SearchedComics = new List<ComicItemViewModel>();
            var result = await _comicService.SearchComicsAsync(SelectedComicSource.Name, _searchText,
                _appliedFilters ?? new Dictionary<string, IReadOnlyList<string>>());

            if (result.IsSuccess)
                SearchedComics = result.Value!.Select(comic => new ComicItemViewModel(comic)).ToList();
            else
                _notificationService.ShowError(result.Error!);

            IsContentLoading = false;
        }

        async partial void OnSelectedComicSourceChanged(ComicSourceModel? value)
        {
            if (value == null) return;

            await UpdateAvailableFiltersAsync();
            await UpdateDisplayedComicsAsync();
        }
    }
}
