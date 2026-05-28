using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using Yukari.Core.Models;
using Yukari.Models.DTO;
using Yukari.ViewModels.Dialogs;
using Yukari.Views.Dialogs;

namespace Yukari.Services.UI;

internal class DialogService : IDialogService
{
    private XamlRoot? _xamlRoot;
    private ElementTheme AppTheme =>
        _xamlRoot?.Content is FrameworkElement fe ? fe.RequestedTheme : ElementTheme.Default;

    public void Initialize(XamlRoot root) => _xamlRoot = root;

    public async Task ShowCollectionsManagerAsync()
    {
        ThrowIfXamlRootNotInitialized();

        var dialog = new CollectionsManagerDialog()
        {
            XamlRoot = _xamlRoot,
            RequestedTheme = AppTheme,
        };
        await dialog.ShowAsync();
    }

    public async Task ShowComicCollectionsDialogAsync(ContentKey comicKey, string comicTitle)
    {
        ThrowIfXamlRootNotInitialized();

        var dialog = new ComicCollectionsDialog(comicKey, comicTitle)
        {
            XamlRoot = _xamlRoot,
            RequestedTheme = AppTheme,
        };
        await dialog.ShowAsync();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>?> ShowFiltersDialogAsync(
        IReadOnlyList<Filter> filters,
        IReadOnlyDictionary<string, IReadOnlyList<string>> appliedFilters
    )
    {
        ThrowIfXamlRootNotInitialized();

        var viewModel = new FiltersDialogViewModel(filters, appliedFilters);
        var dialog = new FiltersDialog(viewModel)
        {
            XamlRoot = _xamlRoot,
            RequestedTheme = AppTheme,
        };

        await dialog.ShowAsync();
        return viewModel.GetAppliedFilters();
    }

    public async Task<string?> OpenFilePickerAsync(string fileTypeFilter = "*")
    {
        ThrowIfXamlRootNotInitialized();

        var picker = new FileOpenPicker(_xamlRoot!.ContentIslandEnvironment.AppWindowId)
        {
            FileTypeFilter = { fileTypeFilter },
            SuggestedStartLocation = PickerLocationId.Downloads,
            ViewMode = PickerViewMode.List,
        };

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private void ThrowIfXamlRootNotInitialized()
    {
        if (_xamlRoot == null)
            throw new InvalidOperationException("XamlRoot must be initialized.");
    }
}
