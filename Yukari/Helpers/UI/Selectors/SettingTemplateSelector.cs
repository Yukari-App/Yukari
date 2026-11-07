using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Components.Settings;

namespace Yukari.Helpers.UI.Selectors;

public partial class SettingTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? PasswordTemplate { get; set; }
    public DataTemplate? NumberTemplate { get; set; }
    public DataTemplate? SingleChoiceTemplate { get; set; }
    public DataTemplate? MultipleChoiceTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) =>
        item switch
        {
            ToggleSettingViewModel => ToggleTemplate,
            TextSettingViewModel => TextTemplate,
            PasswordSettingViewModel => PasswordTemplate,
            NumberSettingViewModel => NumberTemplate,
            SingleChoiceSettingViewModel => SingleChoiceTemplate,
            MultipleChoiceSettingViewModel => MultipleChoiceTemplate,
            _ => base.SelectTemplateCore(item, container),
        };
}
