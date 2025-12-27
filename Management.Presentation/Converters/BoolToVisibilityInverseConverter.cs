using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            return isVisible ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                return v != Visibility.Visible;
            }
            return true;
        }
    }
}
