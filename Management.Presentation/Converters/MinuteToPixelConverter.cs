using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class MinuteToPixelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 0.0;
            
            double minutes = values[0] is double d ? d : 0.0;
            bool isCompact = values[1] is bool b && b;
            
            double factor = isCompact ? (20.0 / 15.0) : (40.0 / 15.0);
            return minutes * factor;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
