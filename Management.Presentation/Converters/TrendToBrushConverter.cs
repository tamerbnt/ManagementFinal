using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Management.Domain.Enums;

namespace Management.Presentation.Converters
{
    public class TrendToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOpacityMode = parameter as string == "Opacity";
            string key = "TextSecondaryBrush"; // Stable

            if (value is TrendDirection trend)
            {
                if (trend == TrendDirection.Up) key = "StatusSuccessBrush";
                else if (trend == TrendDirection.Down) key = "StatusErrorBrush";
            }

            var brush = System.Windows.Application.Current.TryFindResource(key) as SolidColorBrush;

            if (isOpacityMode && brush != null)
            {
                // Return a clone with 20% opacity for backgrounds
                var faded = brush.Clone();
                faded.Opacity = 0.2;
                return faded;
            }

            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}