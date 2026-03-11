using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class PasswordStrengthToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int strength)
            {
                return strength switch
                {
                    <= 2 => Brushes.Red,
                    3 => Brushes.Gold,
                    _ => Brushes.Green
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
