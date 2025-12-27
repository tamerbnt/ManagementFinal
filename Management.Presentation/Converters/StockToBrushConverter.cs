using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class StockToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int stock)
            {
                if (stock == 0) return System.Windows.Application.Current.TryFindResource("StatusErrorBrush");
                if (stock <= 10) return System.Windows.Application.Current.TryFindResource("StatusWarningBrush");
                return System.Windows.Application.Current.TryFindResource("StatusSuccessBrush"); // Or TextPrimary
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}