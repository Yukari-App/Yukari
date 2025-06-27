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
            var glyphs = (parameter as string)?.Split(',');

            return new FontIcon { Glyph = isChecked ? glyphs[1].Trim() : glyphs[0].Trim() };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}
