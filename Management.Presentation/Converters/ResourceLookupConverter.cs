using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Looks up a resource key in the application resources.
    /// Used for localizing strings bound in collections (like Sidebar items).
    /// </summary>
    public class ResourceLookupConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string key && !string.IsNullOrEmpty(key))
            {
                var resource = System.Windows.Application.Current.TryFindResource(key);
                return resource ?? key; // Fallback to key if resource not found
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
