using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class BooleanToThemeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDarkMode)
            {
                var resourceKey = isDarkMode ? "Terminology.Settings.Theme.Dark" : "Terminology.Settings.Theme.Light";
                return System.Windows.Application.Current.TryFindResource(resourceKey) as string ?? (isDarkMode ? "Dark Mode" : "Light Mode");
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
