using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Management.Domain.Enums;

namespace Management.Presentation.Converters
{
    public class MemberStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = "StatusNeutralBrush"; // Default (Frozen/Unknown)

            if (value is MemberStatus status)
            {
                switch (status)
                {
                    case MemberStatus.Active: key = "StatusSuccessBrush"; break;
                    case MemberStatus.Pending: key = "StatusWarningBrush"; break;
                    case MemberStatus.Expired:
                    case MemberStatus.Suspended: key = "StatusErrorBrush"; break;
                    case MemberStatus.Frozen: key = "StatusNeutralBrush"; break;
                }
            }
            // Handle String binding fallback if VM exposes string instead of Enum
            else if (value is string s)
            {
                s = s.ToLower();
                if (s == "active") key = "StatusSuccessBrush";
                else if (s == "expired" || s == "suspended") key = "StatusErrorBrush";
                else if (s == "pending") key = "StatusWarningBrush";
            }

            return (System.Windows.Application.Current.TryFindResource(key) as Brush)!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }
}
