using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class LeftMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int marginValue)
            {
                return new Thickness(marginValue, 0, 0, 0);
            }
            if (value is double dMarginValue)
            {
                return new Thickness(dMarginValue, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
