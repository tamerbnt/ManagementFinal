using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class TemporalGroupConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                var today = DateTime.Today;
                if (date.Date == today)
                    return "Today";
                if (date.Date == today.AddDays(-1))
                    return "Yesterday";
                if (date.Date > today.AddDays(-7))
                    return date.ToString("dddd");
                
                return "Earlier";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
