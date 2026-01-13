using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;

namespace Yukari.Services.UI
{
    public interface IDialogService
    {
        void Initialize(XamlRoot root);
        Task<IReadOnlyDictionary<string, IReadOnlyList<string>>?> ShowFiltersDialogAsync(IReadOnlyList<Filter> filters, IReadOnlyDictionary<string, IReadOnlyList<string>> appliedFilters);
        Task<string?> OpenFilePickerAsync(string fileTypeFilter = "*");
    }
}
