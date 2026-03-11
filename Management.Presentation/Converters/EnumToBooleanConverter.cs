using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value.ToString() ?? string.Empty;
            string targetValue = parameter.ToString() ?? string.Empty;

            return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || !boolValue || parameter == null)
                return Binding.DoNothing;

            try
            {
                if (targetType == typeof(string))
                {
                    return parameter.ToString() ?? string.Empty;
                }

                string? parameterString = parameter.ToString();
                if (parameterString == null) return Binding.DoNothing;
                return Enum.Parse(targetType, parameterString);
            }
            catch
            {
                return Binding.DoNothing;
            }
        }
    }
}
