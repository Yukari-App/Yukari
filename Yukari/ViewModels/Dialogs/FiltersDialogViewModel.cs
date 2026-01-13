using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Yukari.Core.Models;
using Yukari.ViewModels.Components;

namespace Yukari.ViewModels.Dialogs
{
    public partial class FiltersDialogViewModel : ObservableObject
    {
        [ObservableProperty] public partial List<FilterViewModel> Filters { get; set; }

        public FiltersDialogViewModel(IReadOnlyList<Filter> filters, IReadOnlyDictionary<string, IReadOnlyList<string>> appliedFilters)
        {
            Filters = filters.Select(f => {
                    var fvm = new FilterViewModel(f);
                    if (appliedFilters.TryGetValue(f.Key, out var selectedValues))
                        foreach (var option in fvm.Options)
                            if (selectedValues.Contains(option.Key))
                                option.IsSelected = true;

                    return fvm;
                }).ToList();
        }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> GetAppliedFilters() =>
            Filters.Where(f => f.Options.Any(o => o.IsSelected)).ToDictionary(
                f => f.Key,
                f => f.AllowMultiple
                    ? (IReadOnlyList<string>)f.Options.Where(o => o.IsSelected).Select(o => o.Key).ToList()
                    : new List<string> { f.SelectedOptionIfNotAllowMultiple?.Key! }
            );
    }
}
