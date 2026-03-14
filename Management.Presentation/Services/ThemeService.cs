using System;
using System.ComponentModel;
using System.Windows;
using Management.Application.Interfaces.App;
using Management.Domain.Enums;

namespace Management.Presentation.Services
{
    /// <summary>
    /// FINAL PRODUCTION VERSION - v1.2.0-production
    /// Light Mode Only. Locked.
    /// Includes skip guard as requested in build verification.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        private bool _isDarkMode = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsDarkMode => false; // Always false in production
        public bool IsLightMode => true;  // Always true in production

        public void SetTheme(bool isDark)
        {
            // Skip Guard
            if (_isDarkMode == isDark) return;

            // Forced to false for production
            _isDarkMode = false; 
            
            OnPropertyChanged(nameof(IsDarkMode));
            OnPropertyChanged(nameof(IsLightMode));
        }

        public void SetTheme(FacilityType facilityType)
        {
            // Facility-specific theme loading can be implemented here if needed,
            // but for v1.2 we stick to the primary light theme.
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
