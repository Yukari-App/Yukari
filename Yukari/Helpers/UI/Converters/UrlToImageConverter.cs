using System;
using Microsoft.UI.Xaml.Data;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI.Converters;

public class UrlToImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string imageUrl || string.IsNullOrWhiteSpace(imageUrl))
            return null!;
        return App.GetService<IImageCacheService>().GetImageSource(imageUrl);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
