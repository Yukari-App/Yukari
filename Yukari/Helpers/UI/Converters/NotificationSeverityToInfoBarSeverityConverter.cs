using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using Yukari.Enums;

namespace Yukari.Helpers.UI.Converters
{
    public class NotificationSeverityToInfoBarSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is NotificationSeverity severity)
            {
                return severity switch
                {
                    NotificationSeverity.Info => InfoBarSeverity.Informational,
                    NotificationSeverity.Success => InfoBarSeverity.Success,
                    NotificationSeverity.Warning => InfoBarSeverity.Warning,
                    NotificationSeverity.Error => InfoBarSeverity.Error,
                    _ => InfoBarSeverity.Informational
                };
            }

            return InfoBarSeverity.Informational;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
