using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class TimeSpanToOverdueBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan span)
            {
                if (span.TotalMinutes >= 20)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 68, 68)); // Red
                }
            }
            return new SolidColorBrush(Color.FromRgb(0, 212, 255)); // Normal Accent/Blueish
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
