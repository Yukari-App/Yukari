using Microsoft.UI.Xaml.Data;
using System;

namespace Yukari.Helpers.UI.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            value?.ToString() == parameter?.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}
