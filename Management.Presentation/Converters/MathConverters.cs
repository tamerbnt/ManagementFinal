using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class IsNegativeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d) return d < 0;
            if (value is decimal m) return m < 0;
            if (value is int i) return i < 0;
            if (value is float f) return f < 0;
            if (value is long l) return l < 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsGreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            
            try
            {
                double val = System.Convert.ToDouble(value);
                double param = System.Convert.ToDouble(parameter);
                return val > param;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
