using System;
using System.Globalization;
using System.Windows.Data;
using Management.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.Converters
{
    public class TerminologyConverter : IValueConverter
    {
        private static ITerminologyService? _service;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (_service == null)
            {
                // Accessing the service collection from the application instance
                if (System.Windows.Application.Current is App app)
                {
                    _service = app.ServiceProvider.GetService<ITerminologyService>()!;
                }
            }

            if (_service != null && value is string key)
            {
                // Safety check: Only translate if it looks like a terminology key
                if (key.StartsWith("Terminology.") || key.StartsWith("Strings.") || key.StartsWith("Global."))
                {
                    return _service.GetTerm(key);
                }
            }

            return value!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
