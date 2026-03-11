using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class ColumnToOffsetConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || 
                !(values[0] is int columnIndex) || 
                !(values[1] is double actualWidth) || 
                !(values[2] is int columnCount) || 
                columnCount == 0)
                return 0.0;

            double columnWidth = actualWidth / columnCount;
            return columnIndex * columnWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
