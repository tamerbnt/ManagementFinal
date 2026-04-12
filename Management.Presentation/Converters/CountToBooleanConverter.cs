using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class CountToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            if (value is System.Collections.ICollection collection)
            {
                return collection.Count > 0;
            }
            if (value is System.Collections.IEnumerable enumerable)
            {
                return enumerable.Cast<object>().Any();
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
