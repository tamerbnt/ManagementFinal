using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class BoolToSuccessErrorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool success = value is bool b && b;
            string key = success ? "StatusSuccessBrush" : "StatusErrorBrush";
            return (System.Windows.Application.Current.TryFindResource(key) as System.Windows.Media.Brush)!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}