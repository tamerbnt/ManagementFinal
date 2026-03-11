using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class PlanStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? string.Empty;
            // Active -> Green, Archived -> Gray
            string key = (status == "Active") ? "StatusSuccessBrush" : "StatusNeutralBrush";
            return (System.Windows.Application.Current.TryFindResource(key) as Brush)!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}
