using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Yukari.Core.Models;
using Yukari.Models.DTO;

namespace Yukari.Services.UI;

public interface IDialogService
{
    void Initialize(XamlRoot root);

    Task ShowCollectionsManagerAsync();
    Task ShowComicCollectionsDialogAsync(ContentKey comicKey, string comicTitle);
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>?> ShowFiltersDialogAsync(
        IReadOnlyList<Filter> filters,
        IReadOnlyDictionary<string, IReadOnlyList<string>> appliedFilters
    );
    Task<string?> OpenFilePickerAsync(string[]? fileTypeFilters = null);
}
