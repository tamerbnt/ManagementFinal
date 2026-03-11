using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Converts an integer count into an enumerable range of indices.
    /// Useful for skeleton loaders or repeating items in XAML.
    /// </summary>
    public class RangeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = 0;

            // Priority 1: Use parameter if provided
            if (parameter != null && int.TryParse(parameter.ToString(), out int paramCount))
            {
                count = paramCount;
            }
            // Priority 2: Use bound value if numeric
            else if (value is int intValue)
            {
                count = intValue;
            }
            else if (value != null && int.TryParse(value.ToString(), out int val))
            {
                count = val;
            }

            return Enumerable.Range(0, Math.Max(0, count));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
