using System;
using System.Globalization;
using System.Windows.Data;
using Management.Presentation.Views.Salon;

namespace Management.Presentation.Converters
{
    public class AppointmentArgsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is DateTime time && values[1] is Guid staffId)
            {
                return new SalonBookArgs
                {
                    Time = time,
                    StaffId = staffId
                };
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
