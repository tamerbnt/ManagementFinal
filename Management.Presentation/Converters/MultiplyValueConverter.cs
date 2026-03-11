using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// A single-value converter that multiplies incoming value by a ConverterParameter.
    /// Used correctly inside a standard <Binding> tag.
    /// </summary>
    public class MultiplyValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0.0;

            double val = 0.0;
            if (value is int i) val = i;
            else if (value is double d) val = d;
            else if (value is float f) val = f;
            else if (value is decimal m) val = (double)m;
            else return 0.0;

            double multiplier = 1.0;
            if (parameter != null)
            {
                if (parameter is double paramDouble)
                {
                    multiplier = paramDouble;
                }
                else if (parameter is int paramInt)
                {
                    multiplier = paramInt;
                }
                else if (parameter is string paramString && double.TryParse(paramString, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedParam))
                {
                    multiplier = parsedParam;
                }
            }

            return val * multiplier;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
