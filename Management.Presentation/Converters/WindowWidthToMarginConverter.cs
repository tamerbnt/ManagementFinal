using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class WindowWidthToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // Breakpoints match Design System Section 11.1
                if (width < 1280) return new Thickness(32, 40, 32, 32); // Compact
                if (width < 1440) return new Thickness(48, 40, 48, 32); // Standard
                return new Thickness(64, 40, 64, 32);                   // Wide
            }
            return new Thickness(48, 40, 48, 32);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}
