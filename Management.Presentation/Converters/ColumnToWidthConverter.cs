using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class ColumnToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || 
                !(values[0] is double actualWidth) || 
                !(values[1] is int columnCount) || 
                columnCount == 0)
                return 0.0;

            return actualWidth / columnCount;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
