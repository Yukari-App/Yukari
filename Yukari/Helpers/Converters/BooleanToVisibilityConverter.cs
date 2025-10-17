using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Yukari.Helpers.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                    boolValue = !boolValue;

                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}