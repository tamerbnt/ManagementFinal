using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        public bool DefaultToZeroIsVisible { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = value is int i ? i : 0;

            // Check for inversion parameter (used for Empty States where 0 = Visible)
            bool zeroIsVisible = DefaultToZeroIsVisible;
            
            if (parameter is string s)
            {
                zeroIsVisible = s.Equals("ZeroIsVisible", StringComparison.OrdinalIgnoreCase) ||
                                s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            }

            if (zeroIsVisible)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            // Default: Visible if Count > 0 (used for Badges)
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
