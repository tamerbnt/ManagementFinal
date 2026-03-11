using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Management.Presentation.Converters
{
    public class NameToBrushConverter : IValueConverter
    {
        private static readonly Color[] AvatarColors = new Color[]
        {
            Color.FromRgb(59, 130, 246),  // blue-500
            Color.FromRgb(16, 185, 129),  // emerald-500
            Color.FromRgb(245, 158, 11),  // amber-500
            Color.FromRgb(239, 68, 68),   // red-500
            Color.FromRgb(139, 92, 246),  // violet-500
            Color.FromRgb(236, 72, 153),  // pink-500
            Color.FromRgb(14, 165, 233),  // sky-500
            Color.FromRgb(168, 85, 247),  // purple-500
            Color.FromRgb(20, 184, 166)   // teal-500
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrWhiteSpace(name))
            {
                int hash = Math.Abs(name.GetHashCode());
                int index = hash % AvatarColors.Length;
                
                var color = AvatarColors[index];
                var brush = new SolidColorBrush(color);
                brush.Freeze(); // Optimization
                return brush;
            }

            return new SolidColorBrush(Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
