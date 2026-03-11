using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasValue = value != null;

            // Treat empty strings as null
            if (value is string s && string.IsNullOrWhiteSpace(s))
                hasValue = false;

            bool invert = Inverted;
            
            // Allow override via parameter
            if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                invert = true;

            if (invert)
                return hasValue ? Visibility.Collapsed : Visibility.Visible;

            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
