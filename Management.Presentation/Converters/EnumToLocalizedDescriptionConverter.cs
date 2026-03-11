using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class EnumToLocalizedDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var type = value.GetType();
            if (!type.IsEnum) return value.ToString();

            var key = $"Terminology.Enum.{type.Name}.{value}";
            
            try
            {
                var resource = System.Windows.Application.Current.TryFindResource(key);
                return resource as string ?? value.ToString();
            }
            catch
            {
                return value.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
