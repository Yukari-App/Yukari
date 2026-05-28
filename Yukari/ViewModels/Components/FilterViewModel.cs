using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Core.Models;

namespace Yukari.ViewModels.Components;

public partial class FilterViewModel : ObservableObject
{
    public string Key { get; }
    public string DisplayName { get; }
    public bool AllowMultiple { get; }

    public ObservableCollection<ToggleOptionViewModel> Options { get; }

    // OnPropertyChanged is intentionally omitted here.
    // Notifying this property in a TwoWay ComboBox binding causes an infinite loop
    // because the binding re-sets the value on every notification.
    // The ComboBox already has the correct value when the setter is called,
    // so no notification is needed.
    public ToggleOptionViewModel? SelectedOptionIfNotAllowMultiple
    {
        get => Options.FirstOrDefault(o => o.IsSelected);
        set
        {
            if (value is null)
                return;

            foreach (var opt in Options)
                opt.IsSelected = false;

            value.IsSelected = true;
        }
    }

    public FilterViewModel(Filter filter, IReadOnlyList<string>? selectedOptions = null)
    {
        Key = filter.Key;
        DisplayName = filter.DisplayName;
        AllowMultiple = filter.AllowMultiple;

        Options = new ObservableCollection<ToggleOptionViewModel>(
            (filter.Options ?? Enumerable.Empty<FilterOption>()).Select(
                o => new ToggleOptionViewModel(
                    o.Key,
                    selectedOptions?.Contains(o.Key) ?? false,
                    o.DisplayName
                )
            )
        );
    }
}
