using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;

namespace Yukari.Helpers.Converters
{
    public partial class ToggleIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isChecked = value as bool? == true;
            var parameters = (parameter as string)?.Split(',');

            if (parameters.Length == 2)
            {
                return new FontIcon { Glyph = isChecked ? parameters[1].Trim() : parameters[0].Trim() };
            }
            else
            {
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };

                stackPanel.Children.Add(new FontIcon { Glyph = isChecked ? parameters[1].Trim() : parameters[0].Trim(), FontSize = 16 });
                stackPanel.Children.Add(new TextBlock { Text = isChecked ? parameters[3].Trim() : parameters[2].Trim() });

                return stackPanel;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}
