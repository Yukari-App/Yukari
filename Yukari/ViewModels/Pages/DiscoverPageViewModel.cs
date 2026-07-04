using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Core.Models;
using Yukari.Helpers;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Pages;

public partial class DiscoverPageViewModel
    : ObservableObject,
        IRecipient<SearchChangedMessage>,
        IRecipient<ComicSourcesUpdatedMessage>
{
    private readonly IComicService _comicService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IMessenger _messenger;
    private readonly ILocalizationService _localizationService;

    private bool _isDirty = false;
    private bool _isActive = false;

    private IReadOnlyList<Filter>? _availableFilters;
    private IReadOnlyDictionary<string, IReadOnlyList<string>>? _appliedFilters;

    private string _searchText = string.Empty;
    private CancellationTokenSource _searchCts = new();

    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoSources), nameof(NoResults))]
    public partial List<ComicSourceModel>? ComicSources { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ComicItemViewModel> SearchedComics { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceOutdated))]
    public partial ComicSourceModel? SelectedComicSource { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible), nameof(NoSources), nameof(NoResults))]
    [NotifyCanExecuteChangedFor(nameof(FilterCommand), nameof(LoadMoreCommand))]
    public partial bool IsContentLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible))]
    [NotifyCanExecuteChangedFor(nameof(FilterCommand), nameof(LoadMoreCommand))]
    public partial bool IsLoadingMore { get; set; }

    public bool IsSourceOutdated => SelectedComicSource?.Version.IsCoreOutdated() ?? false;

    public bool IsLoadMoreVisible => !IsContentLoading && !NoResults && !NoSources;

    public bool NoSources => !IsContentLoading && (ComicSources == null || ComicSources.Count == 0);
    public bool NoResults => !IsContentLoading && !NoSources && SearchedComics.Count == 0;

    public DiscoverPageViewModel(
        IComicService comicService,
        ISettingsService settingsService,
        IDialogService dialogService,
        INotificationService notificationService,
        IMessenger messenger,
        ILocalizationService localizationService
    )
    {
        _comicService = comicService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _messenger = messenger;
        _localizationService = localizationService;

        _messenger.Register<ComicSourcesUpdatedMessage>(this);
    }

    public void Receive(SearchChangedMessage message)
    {
        _searchText = message.SearchText ?? string.Empty;
        _ = UpdateDisplayedComicsAsync();
    }

    public void Receive(ComicSourcesUpdatedMessage message)
    {
        if (_isActive)
            _ = UpdateAvailableComicSources();
        else
            _isDirty = true;
    }

    public void OnNavigatedTo()
    {
        _isActive = true;
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
        _isActive = false;
    }

    private bool CanFilter() =>
        _availableFilters != null && _availableFilters.Count > 0 && !IsContentLoading;

    [RelayCommand(CanExecute = nameof(CanFilter))]
    private async Task OnFilter()
    {
        if (_availableFilters == null)
            return;

        var newAppliedFilters = await _dialogService.ShowFiltersDialogAsync(
            _availableFilters,
            _appliedFilters ?? new Dictionary<string, IReadOnlyList<string>>()
        );

        if (newAppliedFilters == null)
            return;

        _appliedFilters = newAppliedFilters;
        await UpdateDisplayedComicsAsync();
    }

    private bool CanLoadMore() => !IsContentLoading && !IsLoadingMore && !NoResults;

    [RelayCommand(CanExecute = nameof(CanLoadMore))]
    private async Task LoadMore() => await UpdateDisplayedComicsAsync(append: true);

    [RelayCommand]
    private void NavigateToComic(ContentKey ComicKey) =>
        _messenger.Send(new NavigateMessage(typeof(Views.Pages.ComicPage), ComicKey));

    private async Task UpdateAvailableComicSources()
    {
        var result = await _comicService.GetComicSourcesAsync();
        if (result.IsSuccess)
        {
            ComicSources = result.Value?.Where(s => s.IsEnabled).ToList();

            if (
                SelectedComicSource == null
                || !ComicSources!.Any(x => x.Name == SelectedComicSource.Name)
            )
            {
                SelectedComicSource =
                    ComicSources?.FirstOrDefault(s =>
                        s.Name == _settingsService.Current.DefaultComicSourceName
                    ) ?? ComicSources?.FirstOrDefault();
            }
        }
        else
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
        }
    }

    private async Task UpdateAvailableFiltersAsync()
    {
        if (SelectedComicSource == null)
            return;
        _appliedFilters = null;

        var result = await _comicService.GetSourceFiltersAsync(SelectedComicSource.Name);

        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        _availableFilters = result.Value!;

        var defaultFilters = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var filter in _availableFilters)
        {
            if (filter.AllowMultiple)
            {
                var defaultOpts = filter.Options.Where(o => o.Default).Select(o => o.Key).ToList();
                if (defaultOpts.Count > 0)
                    defaultFilters[filter.Key] = defaultOpts;
            }
            else
            {
                var defaultOpt = filter.Options.FirstOrDefault(o => o.Default);
                if (defaultOpt != null)
                    defaultFilters[filter.Key] = [defaultOpt.Key];
            }
        }

        _appliedFilters = defaultFilters;
        FilterCommand.NotifyCanExecuteChanged();
    }

    private async Task UpdateDisplayedComicsAsync(bool append = false)
    {
        if (SelectedComicSource == null)
            return;

        _searchCts.Cancel();
        _searchCts.Dispose();
        _searchCts = new CancellationTokenSource();

        if (!append)
        {
            IsContentLoading = true;
            _currentPage = 1;
            SearchedComics.Clear();
        }
        else
        {
            IsLoadingMore = true;
            _currentPage++;
        }

        var result = await _comicService.SearchComicsAsync(
            SelectedComicSource.Name,
            _searchText,
            _appliedFilters ?? new Dictionary<string, IReadOnlyList<string>>(),
            _currentPage,
            _searchCts.Token
        );

        if (result.IsCancelled)
            return;

        if (result.IsSuccess)
        {
            if (!append)
            {
                // Replacing the entire collection triggers the GridView entrance animation,
                // which is desirable for the initial page load but not for incremental loading.
                SearchedComics = new(result.Value!.Select(c => new ComicItemViewModel(c)));
            }
            else
            {
                foreach (var comic in result.Value!)
                {
                    // Adding items individually preserves the current scroll position.
                    // Unlike a full collection replacement, this does not trigger the
                    // GridView entrance animation, which is exactly what we want here.
                    SearchedComics.Add(new ComicItemViewModel(comic));
                }
                if (result.Value!.Count == 0)
                {
                    _currentPage--;
                    _notificationService.ShowWarning(
                        _localizationService.GetString("WarningReachedResultsLimitMessage"),
                        _localizationService.GetString("WarningReachedResultsLimitTitle")
                    );
                }
            }
        }
        else
        {
            if (append)
                _currentPage--;
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
        }

        IsContentLoading = false;
        IsLoadingMore = false;
    }

    async partial void OnSelectedComicSourceChanged(ComicSourceModel? value)
    {
        SearchedComics.Clear();
        _availableFilters = null;
        _appliedFilters = null;

        if (value == null)
            return;

        IsContentLoading = true;
        await UpdateAvailableFiltersAsync();
        await UpdateDisplayedComicsAsync();
    }
}
