using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Converts DateTime to relative time strings (e.g. "5m ago").
    /// Implements Design System Section 41.3 (Relative Time).
    /// </summary>
    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                // Ensure we compare UTC to UTC or Local to Local
                // Assuming Domain uses UTC, we convert to Local for display math
                var localDate = date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;
                var now = DateTime.Now;
                var ts = now - localDate;

                if (ts.TotalSeconds < 60) return "Just now";
                if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes}m ago";
                if (ts.TotalHours < 24) return $"{(int)ts.TotalHours}h ago";
                if (ts.TotalDays < 7) return $"{(int)ts.TotalDays}d ago";

                // Fallback to Short Date
                return localDate.ToString("MMM dd");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}