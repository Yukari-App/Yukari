using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.ViewModels.Dialogs;
using Yukari.Views.Dialogs;

namespace Yukari.Services.UI
{
    internal class DialogService : IDialogService
    {
        private XamlRoot? _xamlRoot;

        public void Initialize(XamlRoot root) =>
            _xamlRoot = root;

        public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>?> ShowFiltersDialogAsync(
            IReadOnlyList<Filter> filters, IReadOnlyDictionary<string, IReadOnlyList<string>> appliedFilters)
        {
            FiltersDialogViewModel viewModel = new(filters, appliedFilters);

            var dialog = new FiltersDialog(viewModel)
            {
                XamlRoot = _xamlRoot ?? throw new InvalidOperationException("XamlRoot must be initialized.")
            };

            await dialog.ShowAsync();
            return viewModel.GetAppliedFilters();
        }
    }
}
