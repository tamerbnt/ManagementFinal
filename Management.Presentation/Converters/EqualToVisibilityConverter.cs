using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class EqualToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Inverted ? Visibility.Visible : Visibility.Collapsed;

            bool isEqual = value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            
            if (Inverted)
                return isEqual ? Visibility.Collapsed : Visibility.Visible;
            
            return isEqual ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
