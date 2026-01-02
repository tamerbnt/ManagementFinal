using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        // Allow setting a default parameter on the converter instance (so XAML can set ConverterParameter on the resource)
        public string? ConverterParameter { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Binding-level parameter takes precedence; fall back to instance-level ConverterParameter if provided
            var param = parameter as string ?? ConverterParameter;

            bool isVisible = value is bool b && b;

            if (param is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                isVisible = !isVisible;

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var param = parameter as string ?? ConverterParameter;

            if (value is Visibility v)
            {
                bool result = v == Visibility.Visible;
                if (param is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                    return !result;
                return result;
            }
            return false;
        }
    }
}