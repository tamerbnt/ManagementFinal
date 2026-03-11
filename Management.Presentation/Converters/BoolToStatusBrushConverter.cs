using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class BoolToStatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Used for Integrations: True = Connected (Green), False = Disconnected (Gray)
            bool connected = value is bool b && b;
            string key = connected ? "StatusSuccessBrush" : "StatusNeutralBrush";
            return (System.Windows.Application.Current.TryFindResource(key) as Brush)!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}
