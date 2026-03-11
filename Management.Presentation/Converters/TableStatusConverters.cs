using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Management.Domain.Models.Restaurant;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Maps domain <see cref="TableStatus"/> to the corresponding brush
    /// from Branding.Restaurant.xaml — zero hardcoded colors in the converter.
    /// </summary>
    public class TableStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TableStatus status)
                return DependencyProperty.UnsetValue;

            var resourceKey = status switch
            {
                TableStatus.Available     => "Brush.Table.Available",
                TableStatus.Occupied      => "Brush.Table.Seated",
                TableStatus.OrderSent     => "Brush.Table.Ordered",
                TableStatus.Ready         => "Brush.Table.BillReady",
                TableStatus.BillRequested => "Brush.Table.BillReady",
                TableStatus.Cleaning      => "Brush.Table.Cleaning",
                TableStatus.Dirty         => "Brush.Table.Cleaning",
                _                         => "Brush.Table.Available"
            };

            return System.Windows.Application.Current.TryFindResource(resourceKey) as Brush
                   ?? Brushes.LightGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// White foreground for occupied/active states, dark for available.
    /// </summary>
    public class TableStatusToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TableStatus status && status == TableStatus.Available)
                return System.Windows.Application.Current.TryFindResource("Brush.Text.Secondary") as Brush
                       ?? Brushes.Gray;

            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
