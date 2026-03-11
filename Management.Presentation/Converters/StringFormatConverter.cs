using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class StringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            if (parameter is string resourceKey)
            {
                var formatString = System.Windows.Application.Current.TryFindResource(resourceKey) as string;
                if (!string.IsNullOrEmpty(formatString))
                {
                    try
                    {
                        return string.Format(culture, formatString, value);
                    }
                    catch
                    {
                        return value.ToString();
                    }
                }
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
