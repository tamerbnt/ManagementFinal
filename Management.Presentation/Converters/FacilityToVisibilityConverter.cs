using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Management.Presentation.Services;
using Management.Domain.Enums;

namespace Management.Presentation.Converters
{
    public class FacilityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FacilityType currentFacility && parameter is string targetFacilityStr)
            {
                if (Enum.TryParse<FacilityType>(targetFacilityStr, out var targetFacility))
                {
                    bool isInverse = false;
                    if (targetFacilityStr.StartsWith("!"))
                    {
                        isInverse = true;
                        Enum.TryParse<FacilityType>(targetFacilityStr.Substring(1), out targetFacility);
                    }

                    bool match = currentFacility == targetFacility;
                    if (isInverse) match = !match;

                    return match ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
