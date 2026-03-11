using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class EqualityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            // Check for potential binding errors (UnsetValue)
            if (values.Any(v => v == System.Windows.DependencyProperty.UnsetValue))
                return false;

            var firstValue = values[0];
            return values.Skip(1).All(v => 
                (v == null && firstValue == null) || 
                (v != null && v.Equals(firstValue)) ||
                (v != null && firstValue != null && v.ToString().Equals(firstValue.ToString(), StringComparison.OrdinalIgnoreCase))
            );
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
