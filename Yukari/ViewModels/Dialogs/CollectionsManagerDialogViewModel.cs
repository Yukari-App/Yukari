using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Dialogs;

public partial class CollectionsManagerDialogViewModel : ObservableObject
{
    private const int MaxCollectionNameLength = 24;

    private readonly IComicService _comicService;
    private readonly INotificationService _notificationService;

    private CancellationTokenSource _cts = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoCollections))]
    public partial CollectionItemViewModel[] Collections { get; set; } =
        Array.Empty<CollectionItemViewModel>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCollectionCommand))]
    public partial string NewCollectionName { get; set; } = string.Empty;

    public bool NoCollections => Collections.Length == 0;

    public CollectionsManagerDialogViewModel(
        IComicService comicService,
        INotificationService notificationService
    )
    {
        _comicService = comicService;
        _notificationService = notificationService;

        _ = UpdateCollectionsAsync();
    }

    private bool CanAddCollection() =>
        !string.IsNullOrWhiteSpace(NewCollectionName)
        && NewCollectionName.Length <= MaxCollectionNameLength;

    [RelayCommand(CanExecute = nameof(CanAddCollection))]
    private async Task AddCollectionAsync()
    {
        var result = await _comicService.CreateCollectionAsync(NewCollectionName);
        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        NewCollectionName = string.Empty;
        await UpdateCollectionsAsync();
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync(string collectionName)
    {
        var result = await _comicService.RemoveCollectionAsync(collectionName);
        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        await UpdateCollectionsAsync();
    }

    private async Task UpdateCollectionsAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new();

        var result = await _comicService.GetCollectionsAsync(_cts.Token);
        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        Collections = result
            .Value!.Select(collection => new CollectionItemViewModel(
                _comicService,
                _notificationService,
                MaxCollectionNameLength,
                collection,
                DeleteCollectionCommand
            ))
            .ToArray();
    }
}
