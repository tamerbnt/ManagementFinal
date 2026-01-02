using System;
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
        public static void SetTheme(AppTheme theme)
        {
            var themeUri = theme == AppTheme.Light 
                ? new Uri("Resources/Theme.Light.xaml", UriKind.Relative)
                : new Uri("Resources/Theme.Dark.xaml", UriKind.Relative);

            UpdateDictionary(1, themeUri); // Index 1 is Workspace Layer
        }

        public static void SetFacility(FacilityType facility)
        {
            Uri facilityUri;
            switch (facility)
            {
                case FacilityType.Salon:
                    facilityUri = new Uri("Resources/Branding.Salon.xaml", UriKind.Relative);
                    break;
                case FacilityType.Restaurant:
                    facilityUri = new Uri("Resources/Branding.Restaurant.xaml", UriKind.Relative);
                    break;
                case FacilityType.Gym:
                default:
                    facilityUri = new Uri("Resources/Branding.Gym.xaml", UriKind.Relative);
                    break;
            }

            UpdateDictionary(2, facilityUri); // Index 2 is Identity Layer
        }

        private static void UpdateDictionary(int index, Uri resourceUri)
        {
            var dictionary = new ResourceDictionary { Source = resourceUri };
            if (System.Windows.Application.Current.Resources.MergedDictionaries.Count > index)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[index] = dictionary;
            }
        }
    }
}
