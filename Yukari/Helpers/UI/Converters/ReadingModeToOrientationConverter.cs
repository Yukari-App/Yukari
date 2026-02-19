using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using Yukari.Enums;

namespace Yukari.Helpers.UI.Converters
{
    public class ReadingModeToOrientationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ReadingMode mode)
            {
                return mode == ReadingMode.Vertical
                    ? Orientation.Vertical
                    : Orientation.Horizontal;
            }
            return Orientation.Horizontal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
