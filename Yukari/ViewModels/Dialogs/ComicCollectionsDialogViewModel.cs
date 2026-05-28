using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Dialogs;

public partial class ComicCollectionsDialogViewModel : ObservableObject
{
    private readonly IComicService _comicService;
    private readonly INotificationService _notificationService;

    private ContentKey? _comicKey;

    [ObservableProperty]
    public partial string? ComicTitle { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoCollections))]
    public partial ToggleOptionViewModel[] Collections { get; set; } =
        Array.Empty<ToggleOptionViewModel>();

    public bool NoCollections => Collections.Length == 0;

    public ComicCollectionsDialogViewModel(
        IComicService comicService,
        INotificationService notificationService
    )
    {
        _comicService = comicService;
        _notificationService = notificationService;
    }

    public async Task InitializeAsync(ContentKey comicKey, string comicTitle)
    {
        _comicKey = comicKey;
        ComicTitle = comicTitle;
        await LoadCollections();
    }

    [RelayCommand]
    private async Task OnOptionToggled(ToggleOptionViewModel option)
    {
        if (option.IsSelected)
            await AddToCollection(option.Key);
        else
            await RemoveFromCollection(option.Key);
    }

    private async Task LoadCollections()
    {
        var comicResult = await _comicService.GetComicUserDataAsync(_comicKey!);
        if (!comicResult.IsSuccess)
        {
            _notificationService.ShowError(comicResult.Error!, comicResult.ErrorTitle!);
            return;
        }

        var comicCollections = comicResult.Value!.Collections;

        var collectionsResult = await _comicService.GetCollectionsAsync();
        if (!collectionsResult.IsSuccess)
        {
            _notificationService.ShowError(collectionsResult.Error!, collectionsResult.ErrorTitle!);
            return;
        }

        Collections = collectionsResult
            .Value!.Select(c => new ToggleOptionViewModel(
                c,
                comicCollections.Contains(c),
                toggleCommand: OptionToggledCommand
            ))
            .ToArray();
    }

    private async Task AddToCollection(string collectionName)
    {
        var result = await _comicService.AddComicToCollectionAsync(_comicKey!, collectionName);
        if (!result.IsSuccess)
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
    }

    private async Task RemoveFromCollection(string collectionName)
    {
        var result = await _comicService.RemoveComicFromCollectionAsync(_comicKey!, collectionName);
        if (!result.IsSuccess)
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
    }
}
