using System;
using System.Globalization;
using System.Windows;
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

    public class GridStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d) return new GridLength(Math.Max(0.1, d), GridUnitType.Star);
            if (value is int i) return new GridLength(Math.Max(0.1, i), GridUnitType.Star);
            if (value is string s && double.TryParse(s, out double ds)) return new GridLength(Math.Max(0.1, ds), GridUnitType.Star);
            return new GridLength(1, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
