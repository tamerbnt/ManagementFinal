using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// A single-value converter that multiplies incoming value (e.g. index) by a ConverterParameter (e.g. column width).
    /// Used correctly inside a standard <Binding> tag unlike MultiplyConverter which expects a <MultiBinding>.
    /// </summary>
    public class IndexToOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0.0;

            double index = 0.0;
            if (value is int i) index = i;
            else if (value is double d) index = d;
            else if (value is float f) index = f;
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
                else if (parameter is string paramString && double.TryParse(paramString, out double parsedParam))
                {
                    multiplier = parsedParam;
                }
            }

            return index * multiplier;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
