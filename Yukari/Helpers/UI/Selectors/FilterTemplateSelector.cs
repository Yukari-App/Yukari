using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Components;

namespace Yukari.Helpers.UI.Selectors
{
    public partial class FilterTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MultipleFilterTemplate { get; set; }
        public DataTemplate SingleFilterTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is FilterViewModel filter)
                return filter.AllowMultiple ? MultipleFilterTemplate : SingleFilterTemplate;
            return base.SelectTemplateCore(item, container);
        }
    }
}
