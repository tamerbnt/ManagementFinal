using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Formats decimal values into "DA" currency strings with intelligent abbreviation (K, M).
    /// Supports a 'Full' parameter to return the un-abbreviated formatted string.
    /// </summary>
    public class AbbreviatedCurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "0 DA";

            decimal decimalValue;
            try
            {
                decimalValue = System.Convert.ToDecimal(value);
            }
            catch
            {
                return value.ToString() + " DA";
            }

            bool isFullMode = parameter?.ToString()?.Equals("Full", StringComparison.OrdinalIgnoreCase) ?? false;

            if (isFullMode)
            {
                return string.Format(culture, "{0:N0} DA", decimalValue);
            }

            if (decimalValue >= 1000000)
            {
                return (decimalValue / 1000000m).ToString("0.##", culture) + "M DA";
            }
            
            if (decimalValue >= 1000)
            {
                return (decimalValue / 1000m).ToString("0.#", culture) + "K DA";
            }

            return decimalValue.ToString("N0", culture) + " DA";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
