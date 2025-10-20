using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Yukari.Core.Models;

namespace Yukari.ViewModels.Components
{
    public partial class FilterViewModel : ObservableObject
    {
        public string Key { get; }
        public string DisplayName { get; }
        public bool AllowMultiple { get; }

        public ObservableCollection<FilterOptionViewModel> Options { get; }

        public FilterOptionViewModel? SelectedOptionIfNotAllowMultiple
        {
            get => Options.FirstOrDefault(o => o.IsSelected);
            set
            {
                if (value is null) return;

                foreach (var opt in Options)
                    opt.IsSelected = false;

                value.IsSelected = true;

                OnPropertyChanged();
            }
        }

        public FilterViewModel(Filter filter)
        {
            Key = filter.Key;
            DisplayName = filter.DisplayName;
            AllowMultiple = filter.AllowMultiple;

            Options = new ObservableCollection<FilterOptionViewModel>(
                (filter.Options ?? Enumerable.Empty<FilterOption>())
                    .Select(o => new FilterOptionViewModel(o))
            );
        }
    }
}
