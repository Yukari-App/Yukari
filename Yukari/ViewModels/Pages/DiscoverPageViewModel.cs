using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Services.Comics;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class DiscoverPageViewModel : ObservableObject, IRecipient<SearchMessage>, IRecipient<FiltersDialogResultMessage>
    {
        private IComicService _comicService;

        [ObservableProperty] private ObservableCollection<ComicSourceModel> _comicSources = new();
        [ObservableProperty] private ObservableCollection<ComicItemViewModel> _searchedComics = new();
        private IReadOnlyList<Filter> _availableFilters;
        private IReadOnlyDictionary<string, IReadOnlyList<string>> _appliedFilters;

        [ObservableProperty] private ComicSourceModel _selectedComicSource;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(NoResults)), NotifyCanExecuteChangedFor(nameof(FilterCommand))]
        private bool _isContentLoading = true;

        private string _searchText;

        public bool NoResults => !IsContentLoading && !SearchedComics.Any();

        public DiscoverPageViewModel(IComicService comicService)
        {
            WeakReferenceMessenger.Default.Register<SearchMessage>(this);
            WeakReferenceMessenger.Default.Register<FiltersDialogResultMessage>(this);

            _comicService = comicService;
        }

        public async void Receive(SearchMessage message)
        {
            _searchText = message.SearchText ?? string.Empty;
            await UpdateDisplayedComicsAsync();
        }
        public async void Receive(FiltersDialogResultMessage message)
        {
            _appliedFilters = message.AppliedFilters;
            await UpdateDisplayedComicsAsync();
        }

        public async Task InitializeAsync()
        {
            ComicSources = new ObservableCollection<ComicSourceModel>(await _comicService.GetComicSourcesAsync());
            SelectedComicSource = ComicSources.FirstOrDefault(); // will call OnSelectedComicSourceChanged
        }

        private async Task UpdateDisplayedComicsAsync()
        {
            SearchedComics.Clear();

            IsContentLoading = true;
            var comics = await _comicService.SearchComicsAsync(SelectedComicSource.Name, _searchText, _appliedFilters);

            SearchedComics = new ObservableCollection<ComicItemViewModel>(
               comics.Select(comic => new ComicItemViewModel(comic, _comicService))
            );
            IsContentLoading = false;
        }

        [RelayCommand]
        private void NavigateToComic(ContentIdentifier comicIdentifier)
        {
            WeakReferenceMessenger.Default.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), comicIdentifier));
        }

        [RelayCommand(CanExecute = nameof(CanFilter))]
        private void OnFilter() =>
            WeakReferenceMessenger.Default.Send(new RequestFiltersDialogMessage(_availableFilters, _appliedFilters));

        private bool CanFilter() =>
            (_availableFilters?.Count > 0) && !IsContentLoading;

        async partial void OnSelectedComicSourceChanged(ComicSourceModel value)
        {
            _availableFilters = await _comicService.GetSourceFiltersAsync(value.Name);
            _appliedFilters = new Dictionary<string, IReadOnlyList<string>>();

            await UpdateDisplayedComicsAsync();
        }
    }
}
