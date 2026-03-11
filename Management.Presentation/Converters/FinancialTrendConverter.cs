using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class FinancialTrendConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not decimal percentage) 
            {
                // Handle double just in case
                if (value is double d) percentage = (decimal)d;
                else return Binding.DoNothing;
            }

            string param = parameter as string ?? "Profit";
            bool isPositive = percentage > 0;
            bool isNegative = percentage < 0;

            // Determine context (Good vs Bad)
            bool isGood = param.Contains("Expenses") ? isNegative : isPositive;
            bool isBad = param.Contains("Expenses") ? isPositive : isNegative;

            // Return Icon (Path Data)
            if (param.Contains("Icon"))
            {
                return isPositive ? "M2,8 L6,2 L10,8" : "M2,2 L6,8 L10,2";
            }

            // Return Brush
            string colorKey = "TextTertiaryBrush"; 
            if (isGood) colorKey = "StatusSuccessBrush";
            else if (isBad) colorKey = "StatusErrorBrush";

            if (param.Contains("Background"))
            {
                var brush = System.Windows.Application.Current.TryFindResource(colorKey) as SolidColorBrush;
                if (brush != null)
                {
                    var faded = brush.Clone();
                    faded.Opacity = 0.2;
                    return faded;
                }
            }

            return System.Windows.Application.Current.TryFindResource(colorKey) as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
