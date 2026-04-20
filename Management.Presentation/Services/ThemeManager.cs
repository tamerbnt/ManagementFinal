using System;
using System.Linq;
using System.Windows;
using Management.Domain.Enums;

namespace Management.Presentation.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private static AppTheme _currentTheme = AppTheme.Light;
        private static FacilityType _currentFacility = FacilityType.Gym;

        public static void SetTheme(AppTheme theme)
        {
            _currentTheme = theme;
            var themeUri = theme == AppTheme.Light 
                ? new Uri("Resources/Theme.Light.xaml", UriKind.Relative)
                : new Uri("Resources/Theme.Dark.xaml", UriKind.Relative);

            UpdateDictionary("Theme.", themeUri);
            
            // Re-apply facility branding to ensure the theme-specific variant is loaded
            SetFacility(_currentFacility);
        }

        public static void SetFacility(FacilityType facility)
        {
            _currentFacility = facility;
            string themeSuffix = _currentTheme == AppTheme.Dark ? ".Dark" : "";
            Uri facilityUri;
            
            switch (facility)
            {
                case FacilityType.Salon:
                    facilityUri = new Uri($"Resources/Branding.Salon{themeSuffix}.xaml", UriKind.Relative);
                    break;
                case FacilityType.Restaurant:
                    facilityUri = new Uri($"Resources/Branding.Restaurant{themeSuffix}.xaml", UriKind.Relative);
                    break;
                case FacilityType.Gym:
                default:
                    facilityUri = new Uri($"Resources/Branding.Gym{themeSuffix}.xaml", UriKind.Relative);
                    break;
            }

            // Verify if the dark variant actually exists before applying it (fallback to light branding if not)
            // In a real WPF app, we might use a helper to check ResourceDictionary availability,
            // but for now we assume Gym has both.
            UpdateDictionary("Branding.", facilityUri);
        }

        private static void UpdateDictionary(string sourcePart, Uri newResourceUri)
        {
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            
            for (int i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.OriginalString;
                if (source != null && source.Contains(sourcePart))
                {
                    // Basic sanity check: if loading a .Dark branding, make sure we don't accidentally
                    // replace it if we're searching for "Branding." (Contains will match both).
                    // Our loop replaces the FIRST match, which is correct because we only keep ONE branding active.
                    dictionaries[i] = new ResourceDictionary { Source = newResourceUri };
                    return;
                }
            }

            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = newResourceUri });
        }
    }
}


