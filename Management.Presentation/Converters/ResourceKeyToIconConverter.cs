using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Looks up a Geometry resource by its string Key.
    /// Used for dynamic icons in Navigation and Settings.
    /// </summary>
    public class ResourceKeyToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string resourceKey && !string.IsNullOrEmpty(resourceKey))
            {
                return (System.Windows.Application.Current.TryFindResource(resourceKey) as Geometry)!;
            }
            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
