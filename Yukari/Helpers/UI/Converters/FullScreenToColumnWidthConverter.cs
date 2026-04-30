using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Yukari.Helpers.UI.Converters;

public class FullScreenToColumnWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (bool)value ? new GridLength(0) : new GridLength(144);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
