using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class BoolToSuccessErrorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSuccess && isSuccess)
            {
                return new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Success green
            }
            return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Error red
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
