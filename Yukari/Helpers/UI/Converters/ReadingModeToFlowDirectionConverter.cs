using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using Yukari.Enums;

namespace Yukari.Helpers.UI.Converters
{
    public class ReadingModeToFlowDirectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ReadingMode mode)
            {
                return mode == ReadingMode.RightToLeft
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
            }
            return FlowDirection.LeftToRight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
