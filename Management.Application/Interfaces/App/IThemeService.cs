using System.ComponentModel;
using Management.Domain.Enums;

namespace Management.Application.Interfaces.App
{
    public interface IThemeService : INotifyPropertyChanged
    {
        bool IsDarkMode { get; }
        bool IsLightMode { get; }
        void SetTheme(bool isDark);
        void SetTheme(FacilityType facilityType);
    }
}
