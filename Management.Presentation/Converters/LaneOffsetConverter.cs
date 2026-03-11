using System;
using System.Globalization;
using System.Windows.Data;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Computes Canvas.Left for an appointment card, accounting for both
    /// the stylist column and the lane (sub-column) within that column.
    /// Values: [ColumnIndex, LaneIndex, LaneCount, TotalWidth, ColumnCount]
    /// </summary>
    public class LaneOffsetConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 5 ||
                values[0] is not int columnIndex ||
                values[1] is not int laneIndex ||
                values[2] is not int laneCount ||
                values[3] is not double totalWidth ||
                values[4] is not int columnCount ||
                columnCount == 0)
                return 0.0;

            if (laneCount < 1) laneCount = 1;

            double columnWidth = totalWidth / columnCount;
            double laneWidth = columnWidth / laneCount;
            return columnIndex * columnWidth + laneIndex * laneWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Computes the Width for an appointment card — a fraction of the stylist column.
    /// Values: [LaneCount, TotalWidth, ColumnCount]
    /// </summary>
    public class LaneWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 ||
                values[0] is not int laneCount ||
                values[1] is not double totalWidth ||
                values[2] is not int columnCount ||
                columnCount == 0)
                return 250.0;

            if (laneCount < 1) laneCount = 1;

            double columnWidth = totalWidth / columnCount;
            // Leave a small gap between lanes for visual separation
            return Math.Max(60, columnWidth / laneCount - 2);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
