using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Core.Models;

namespace Yukari.ViewModels.Components
{
    public partial class FilterOptionViewModel : ObservableObject
    {
        public string Key { get; }
        public string DisplayName { get; }

        [ObservableProperty] public partial bool IsSelected { get; set; }

        public FilterOptionViewModel(FilterOption option)
        {
            Key = option.Key;
            DisplayName = option.DisplayName;
        }
    }
}
