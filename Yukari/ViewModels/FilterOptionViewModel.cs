using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Core.Models;

namespace Yukari.ViewModels
{
    public partial class FilterOptionViewModel : ObservableObject
    {
        public string Key { get; }
        public string DisplayName { get; }

        [ObservableProperty]
        private bool _isSelected;

        public FilterOptionViewModel(FilterOption option)
        {
            Key = option.Key;
            DisplayName = option.DisplayName;
        }
    }
}
