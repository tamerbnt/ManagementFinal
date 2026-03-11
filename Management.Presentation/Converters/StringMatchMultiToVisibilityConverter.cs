using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class StringMatchMultiToVisibilityConverter : IMultiValueConverter
    {
        public Visibility MatchVisibility { get; set; } = Visibility.Visible;
        public Visibility NoMatchVisibility { get; set; } = Visibility.Collapsed;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length >= 2)
            {
                string? val1 = values[0]?.ToString();
                string? val2 = values[1]?.ToString();

                if (string.Equals(val1, val2, StringComparison.OrdinalIgnoreCase))
                {
                    return MatchVisibility;
                }
            }
            return NoMatchVisibility;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
