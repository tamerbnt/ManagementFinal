using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class RegistrationAgeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = "TextTertiaryBrush";

            if (value is DateTime date)
            {
                // Ensure comparison logic handles UTC/Local consistently
                var age = DateTime.UtcNow - (date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime());

                if (age.TotalHours < 1) key = "StatusSuccessBrush";      // Fresh (<1h)
                else if (age.TotalHours < 24) key = "StatusWarningBrush"; // Warm (<24h)
                else key = "StatusErrorBrush";                           // Cold (>24h)
            }

            return (System.Windows.Application.Current.TryFindResource(key) as Brush)!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}
