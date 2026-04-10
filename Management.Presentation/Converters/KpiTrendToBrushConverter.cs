using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Management.Application.DTOs;

namespace Management.Presentation.Converters
{
    public class KpiTrendToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is KpiMetricDto metric)
            {
                if (metric.PercentChange == 0)
                    return System.Windows.Application.Current.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;

                bool isPositiveChange = metric.PercentChange > 0;
                bool isVisualSuccess = metric.IsIncreasePositive ? isPositiveChange : !isPositiveChange;

                string resourceKey = isVisualSuccess ? "StatusSuccessBrush" : "StatusErrorBrush";
                return System.Windows.Application.Current.TryFindResource(resourceKey) as Brush ?? (isVisualSuccess ? Brushes.Green : Brushes.Red);
            }

            return System.Windows.Application.Current.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}
