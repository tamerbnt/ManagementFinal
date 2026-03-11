using System;
using System.Globalization;
using System.Windows.Data;
using Management.Domain.Enums;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Converts hardware status enums to UI-friendly labels.
    /// Used in TurnstileStatusPanel.
    /// </summary>
    public class TurnstileStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TurnstileStatus status)
            {
                switch (status)
                {
                    case TurnstileStatus.Operational: return "Online";
                    case TurnstileStatus.Locked: return "Locked";
                    case TurnstileStatus.Maintenance: return "Maintenance";
                    case TurnstileStatus.OutOfOrder: return "Error";
                    default: return "Unknown";
                }
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
