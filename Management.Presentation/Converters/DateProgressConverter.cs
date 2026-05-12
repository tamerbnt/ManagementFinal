using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class DateProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is DateTime startDate && values[1] is DateTime expirationDate)
            {
                if (startDate == default || expirationDate == default)
                    return 0.0;

                var totalDuration = (expirationDate - startDate).TotalDays;
                if (totalDuration <= 0)
                    return 100.0; // Assume completed if end is before start

                var elapsed = (DateTime.UtcNow - startDate).TotalDays;

                if (elapsed <= 0)
                    return 0.0;

                if (elapsed >= totalDuration)
                    return 100.0;

                return (elapsed / totalDuration) * 100.0;
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
