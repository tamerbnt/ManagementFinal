using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Management.Domain.Enums;

namespace Management.Presentation.Converters
{
    public class TurnstileStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = "TextTertiaryBrush"; // Default/Unknown
            if (value is TurnstileStatus status)
            {
                switch (status)
                {
                    case TurnstileStatus.Operational: key = "StatusSuccessBrush"; break;
                    case TurnstileStatus.Locked: key = "StatusWarningBrush"; break;
                    case TurnstileStatus.OutOfOrder: key = "StatusErrorBrush"; break;
                    case TurnstileStatus.Maintenance: key = "StatusWarningBrush"; break;
                }
            }
            return (System.Windows.Application.Current.TryFindResource(key) as Brush)!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}