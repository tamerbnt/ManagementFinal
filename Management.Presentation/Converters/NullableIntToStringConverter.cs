using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Converts between <c>int?</c> and <c>string</c> for two-way binding on numeric text inputs.
    /// ConvertBack returns null for empty or non-numeric input so the VM property stays nullable.
    /// </summary>
    public class NullableIntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i ? i.ToString() : string.Empty;

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && int.TryParse(s.Trim(), out int result))
                return result;
            return null;
        }
    }
}
