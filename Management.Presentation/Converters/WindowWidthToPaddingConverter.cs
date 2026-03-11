using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Converts Window Width to Thickness for the Main Content Shell.
    /// Implements Design System Section 11.1 (Responsive Content Padding).
    /// </summary>
    public class WindowWidthToPaddingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // Breakpoint: 1440px
                if (width < 1440)
                {
                    // Compact Padding (Left/Right 32)
                    return new Thickness(32, 40, 32, 32);
                }

                // Standard Padding (Left/Right 48)
                return new Thickness(48, 40, 48, 32);
            }

            // Fallback
            return new Thickness(48, 40, 48, 32);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
