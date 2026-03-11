using System;
using System.ComponentModel;
using System.Windows;
using Management.Application.Interfaces.App;
using Management.Domain.Enums;
using Management.Presentation.Services.Infrastructure;

namespace Management.Presentation.Services.Application
{
    public class ThemeService : IThemeService
    {
        private readonly IPerformanceService _performanceService;
        private bool _isDarkMode;

        public bool IsDarkMode => _isDarkMode;
        public bool IsLightMode => !_isDarkMode;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ThemeService(IPerformanceService performanceService)
        {
            _performanceService = performanceService;
            // Initial load
            SetTheme(false); 
        }

        public void SetTheme(bool isDark)
        {
            _isDarkMode = isDark;
            OnPropertyChanged(nameof(IsDarkMode));
            OnPropertyChanged(nameof(IsLightMode));

            var app = System.Windows.Application.Current;
            if (app == null) return;

            // Load LowFx adjustments if needed
            if (_performanceService.IsLowFxMode)
            {
                ApplyLowFx(app);
            }
        }

        public void SetTheme(FacilityType facilityType)
        {
            var themeName = facilityType switch
            {
                FacilityType.Gym => "Gym",
                FacilityType.Salon => "Salon",
                FacilityType.Restaurant => "Restaurant",
                _ => "Gym"
            };

            var app = System.Windows.Application.Current;
            if (app == null) return;

            try
            {
                var uri = new Uri($"pack://application:,,,/Titan.Client;component/Resources/Themes/{themeName}.xaml");
                var themeDict = new ResourceDictionary { Source = uri };
                
                // Remove existing themes if any (heuristic: look for themes/ folder in source)
                var existing = app.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source?.OriginalString.Contains("/Themes/") == true);
                
                if (existing != null)
                    app.Resources.MergedDictionaries.Remove(existing);

                app.Resources.MergedDictionaries.Add(themeDict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load facility theme: {ex.Message}");
            }
        }

        private void ApplyLowFx(System.Windows.Application app)
        {
            try 
            {
                var lowFxUri = new Uri("pack://application:,,,/Titan.Client;component/Resources/Themes/LowFx.xaml");
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = lowFxUri });
            }
            catch { /* Handle missing dictionary */ }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
