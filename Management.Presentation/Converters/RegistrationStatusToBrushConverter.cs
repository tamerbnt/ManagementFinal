using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Management.Domain.Enums;

namespace Management.Presentation.Converters
{
    public class RegistrationStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not RegistrationStatus status) return Brushes.Transparent;

            var (colorHex, opacity) = status switch
            {
                RegistrationStatus.Pending => ("#3B82F6", 1.0),   // Blue 500
                RegistrationStatus.Approved => ("#10B981", 1.0),  // Emerald 500
                RegistrationStatus.Declined => ("#EF4444", 1.0),  // Red 500
                _ => ("#64748B", 1.0)                             // Slate 500
            };

            // Custom opacity from parameter if provided
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double customOpacity))
            {
                opacity = customOpacity;
            }

            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var brush = new SolidColorBrush(color) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
