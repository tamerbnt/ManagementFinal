using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class CollectionContainsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var collection = values[0] as IEnumerable;
            var item = values[1];

            if (collection == null || item == null)
                return false;

            foreach (var existingItem in collection)
            {
                if (existingItem.Equals(item))
                    return true;
                
                // Fallback for DTOs/Entities that might not have perfect Equals but have IDs
                var idProp = existingItem.GetType().GetProperty("Id");
                var itemIdProp = item.GetType().GetProperty("Id");
                if (idProp != null && itemIdProp != null)
                {
                    var id1 = idProp.GetValue(existingItem);
                    var id2 = itemIdProp.GetValue(item);
                    if (id1 != null && id2 != null && id1.Equals(id2))
                        return true;
                }
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
