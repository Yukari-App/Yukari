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
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages
{
    public partial class DiscoverPageViewModel : ObservableObject,
        IRecipient<SearchChangedMessage>, IRecipient<FiltersDialogResultMessage>, IRecipient<ComicSourcesUpdatedMessage>
    {
        private readonly IComicService _comicService;
        private readonly IMessenger _messenger;

        private IReadOnlyList<Filter> _availableFilters = new List<Filter>();
        private IReadOnlyDictionary<string, IReadOnlyList<string>> _appliedFilters = new Dictionary<string, IReadOnlyList<string>>();

        private string _searchText = string.Empty;

        [ObservableProperty] public partial List<ComicSourceModel> ComicSources { get; set; } = new();
        [ObservableProperty] public partial List<ComicItemViewModel> SearchedComics { get; set; } = new();
        
        [ObservableProperty] public partial ComicSourceModel? SelectedComicSource { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(NoResults)), NotifyCanExecuteChangedFor(nameof(FilterCommand))]
        public partial bool IsContentLoading { get; set; } = true;

        public bool NoResults => !IsContentLoading && !SearchedComics.Any();

        public DiscoverPageViewModel(IComicService comicService, IMessenger messenger)
        {
            _comicService = comicService;
            _messenger = messenger;

            _messenger.Register<FiltersDialogResultMessage>(this);
            _messenger.Register<ComicSourcesUpdatedMessage>(this);
        }
            
        public void RegisterSearchMessages() =>
            _messenger.Register<SearchChangedMessage>(this);

        public void UnregisterSearchMessages() =>
            _messenger.Unregister<SearchChangedMessage>(this);

        public async void Receive(SearchChangedMessage message)
        {
            _searchText = message.SearchText ?? string.Empty;
            await UpdateDisplayedComicsAsync();
        }

        public async void Receive(FiltersDialogResultMessage message)
        {
            _appliedFilters = message.AppliedFilters;
            await UpdateDisplayedComicsAsync();
        }

        public async void Receive(ComicSourcesUpdatedMessage message) =>
            await UpdateAvailableComicSources();

        public async Task LoadDiscoverDataAsync()
        {
            _messenger.Send(new SetSearchTextMessage(_searchText));

            if (ComicSources.Count == 0)
                await UpdateAvailableComicSources();
        }

        private async Task UpdateAvailableComicSources()
        {
            ComicSources = new(await _comicService.GetComicSourcesAsync());

            SelectedComicSource = ComicSources.FirstOrDefault(); // will call OnSelectedComicSourceChanged
        }

        private async Task UpdateDisplayedComicsAsync()
        {
            IsContentLoading = true;

            SearchedComics = new List<ComicItemViewModel>();
            SearchedComics = (await _comicService.SearchComicsAsync(SelectedComicSource!.Name, _searchText, _appliedFilters))
                .Select(comic => new ComicItemViewModel(comic)).ToList();

            IsContentLoading = false;
        }

        [RelayCommand(CanExecute = nameof(CanFilter))]
        private void OnFilter() =>
            _messenger.Send(new RequestFiltersDialogMessage(_availableFilters, _appliedFilters));

        private bool CanFilter() =>
            (_availableFilters?.Count > 0) && !IsContentLoading;

        [RelayCommand]
        private void NavigateToComic(ContentKey ComicKey) =>
            _messenger.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), ComicKey));

        async partial void OnSelectedComicSourceChanged(ComicSourceModel? value)
        {
            if (value == null) return;

            _availableFilters = await _comicService.GetSourceFiltersAsync(value.Name);
            _appliedFilters = new Dictionary<string, IReadOnlyList<string>>();

            await UpdateDisplayedComicsAsync();
        }
    }
}
