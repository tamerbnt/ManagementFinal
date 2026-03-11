using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class StringMatchToVisibilityConverter : IValueConverter
    {
        public Visibility MatchVisibility { get; set; } = Visibility.Visible;
        public Visibility NoMatchVisibility { get; set; } = Visibility.Collapsed;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && parameter is string parameterValue)
            {
                if (string.Equals(stringValue, parameterValue, StringComparison.OrdinalIgnoreCase))
                {
                    return MatchVisibility;
                }
            }
            return NoMatchVisibility;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
