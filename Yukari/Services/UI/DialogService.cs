using System;
using System.Collections.Generic;
using System.Linq;
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

    public async Task ShowLocalComicDialogAsync(ContentKey? comicKey = null)
    {
        ThrowIfXamlRootNotInitialized();

        var dialog = new LocalComicDialog(comicKey)
        {
            XamlRoot = _xamlRoot,
            RequestedTheme = AppTheme,
        };

        await dialog.ShowAsync();
    }

    public async Task ShowExportDialogAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        string comicTitle,
        string chapterTitle
    )
    {
        ThrowIfXamlRootNotInitialized();

        var dialog = new ChapterExportDialog(comicKey, chapterKey, comicTitle, chapterTitle)
        {
            XamlRoot = _xamlRoot,
            RequestedTheme = AppTheme,
        };

        await dialog.ShowAsync();
    }

    public async Task<string?> OpenFilePickerAsync(string[]? fileTypeFilters = null)
    {
        ThrowIfXamlRootNotInitialized();

        var picker = new FileOpenPicker(_xamlRoot!.ContentIslandEnvironment.AppWindowId)
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            ViewMode = PickerViewMode.List,
        };

        if (fileTypeFilters is { Length: > 0 })
        {
            foreach (var ext in fileTypeFilters)
                picker.FileTypeFilter.Add(ext.StartsWith('.') ? ext : "." + ext);
        }
        else
        {
            picker.FileTypeFilter.Add("*");
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> OpenFolderPickerAsync()
    {
        ThrowIfXamlRootNotInitialized();

        var picker = new FolderPicker(_xamlRoot!.ContentIslandEnvironment.AppWindowId)
        {
            SuggestedStartLocation = PickerLocationId.Unspecified,
            ViewMode = PickerViewMode.List,
        };

        var file = await picker.PickSingleFolderAsync();
        return file?.Path;
    }

    public async Task<string?> OpenFileSavePicker(
        string? suggestedFileName = null,
        Dictionary<string, string[]>? fileTypeChoices = null
    )
    {
        ThrowIfXamlRootNotInitialized();

        var picker = new FileSavePicker(_xamlRoot!.ContentIslandEnvironment.AppWindowId)
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = suggestedFileName ?? "File",
        };

        if (fileTypeChoices is { Count: > 0 })
        {
            foreach (var kvp in fileTypeChoices)
            {
                var extensions = kvp.Value.Select(e => e.StartsWith('.') ? e : "." + e).ToList();
                picker.FileTypeChoices.Add(kvp.Key, extensions);
            }
        }

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private void ThrowIfXamlRootNotInitialized()
    {
        if (_xamlRoot == null)
            throw new InvalidOperationException("XamlRoot must be initialized.");
    }
}
