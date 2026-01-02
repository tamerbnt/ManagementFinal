// Management.Presentation/Services/ThemeService.cs
// v1.2 FINAL – LIGHT MODE ONLY – NO SWITCHING ALLOWED
// Exists to satisfy CODE STRUCTURE FINAL.txt – functionality locked

using System.ComponentModel;

namespace Management.Presentation.Services
{
    /// <summary>
    /// Required by official code structure.
    /// In v1.2 Final: Light mode only. No runtime switching. No dark mode.
    /// </summary>
    public interface IThemeService : INotifyPropertyChanged
    {
        bool IsDarkMode { get; }  // Always false
        bool IsLightMode { get; } // Always true
    }

    /// <summary>
    /// Minimal, locked implementation.
    /// Satisfies DI and structure requirements without enabling dark mode.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        #pragma warning disable 0067
        public event PropertyChangedEventHandler? PropertyChanged;
        #pragma warning restore 0067

        public bool IsDarkMode => false;
        public bool IsLightMode => true;

        // No methods. No state. No switching.
        // Dark mode is physically impossible in v1.2.
    }
}
