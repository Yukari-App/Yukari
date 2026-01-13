using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
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

        public async Task<string?> OpenFilePickerAsync(string fileTypeFilter = "*")
        {
            if (_xamlRoot == null) throw new InvalidOperationException("XamlRoot must be initialized.");

            var picker = new FileOpenPicker(_xamlRoot.ContentIslandEnvironment.AppWindowId)
            {
                FileTypeFilter = { fileTypeFilter },
                SuggestedStartLocation = PickerLocationId.Downloads,
                ViewMode = PickerViewMode.List,
            };

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
