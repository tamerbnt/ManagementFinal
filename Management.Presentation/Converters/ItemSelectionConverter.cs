using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Management.Presentation.ViewModels.Restaurant;

namespace Management.Presentation.Converters
{
    public class ItemSelectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not TakeoutSessionViewModel session || values[1] is not string itemName)
                return false;

            return session.IsItemSelected(itemName);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
