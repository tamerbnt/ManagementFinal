using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Implements both IValueConverter and IMultiValueConverter.
    /// As IValueConverter: multiplies the single value by ConverterParameter.
    /// As IMultiValueConverter: multiplies all bound values together.
    /// This dual implementation prevents InvalidCastException when the converter
    /// is referenced in either a plain Binding or a MultiBinding.
    /// </summary>
    public class MultiplyConverter : IMultiValueConverter, IValueConverter
    {
        // --- IMultiValueConverter (used in <MultiBinding>) ---
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double result = 1.0;
            if (values == null) return result;
            foreach (var value in values)
                result *= ToDouble(value);
            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        // --- IValueConverter (used in plain <Binding>) ---
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = ToDouble(value);
            double multiplier = ToDouble(parameter);
            if (multiplier == 0) multiplier = 1.0;
            return val * multiplier;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private double ToDouble(object val)
        {
            if (val is double d) return d;
            if (val is int i) return i;
            if (val is float f) return f;
            if (val is decimal m) return (double)m;
            if (val is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double sd)) return sd;
            return 1.0;
        }
    }
}
