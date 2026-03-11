using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public bool Inverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = value is bool b && b;

            // Handle inversion
            if (Inverted || (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase)))
            {
                isActive = !isActive;
            }

            if (isActive)
            {
                return new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)); // Dark slate for active
            }
            return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)); // Muted gray for inactive
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
