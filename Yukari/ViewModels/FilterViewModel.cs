using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Yukari.Core.Models;

namespace Yukari.ViewModels
{
    public partial class FilterViewModel : ObservableObject
    {
        public string Key { get; }
        public string DisplayName { get; }
        public bool AllowMultiple { get; }

        public ObservableCollection<FilterOptionViewModel> Options { get; }

        public FilterViewModel(Filter filter)
        {
            Key = filter.Key;
            DisplayName = filter.DisplayName;
            AllowMultiple = filter.AllowMultiple;

            Options = new ObservableCollection<FilterOptionViewModel>(
                filter.Options.Select(o => new FilterOptionViewModel(o))
            );
        }
    }
}
