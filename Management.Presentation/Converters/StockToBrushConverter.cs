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
                return (System.Windows.Application.Current.TryFindResource(stock == 0 ? "StatusErrorBrush" : (stock <= 10 ? "StatusWarningBrush" : "StatusSuccessBrush")) as Brush)!;
            }
            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}