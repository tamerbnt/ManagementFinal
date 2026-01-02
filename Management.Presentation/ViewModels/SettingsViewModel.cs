using System.Threading.Tasks;
using System.Windows.Input;
using Management.Presentation.Extensions;
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityService;

        private string _facilityName = string.Empty;
        public string FacilityName
        {
            get => _facilityName;
            set
            {
                if (SetProperty(ref _facilityName, value))
                {
                    _ = AutoSaveAsync();
                }
            }
        }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    _ = AutoSaveAsync();
                }
            }
        }

        private double _masterVolume;
        public double MasterVolume
        {
            get => _masterVolume;
            set
            {
                if (SetProperty(ref _masterVolume, value))
                {
                    _ = AutoSaveAsync();
                }
            }
        }

        public SettingsViewModel(INotificationService notificationService, Management.Domain.Services.IFacilityContextService facilityService)
        {
            _notificationService = notificationService;
            _facilityService = facilityService;
            
            // Initial data
            FacilityName = "Titan Performance Center";
            IsDarkMode = true;
            MasterVolume = 80;
        }

        private async Task AutoSaveAsync()
        {
            // Debounce logic could be added here
            await Task.Delay(500);
            
            // Trigger the "Saved ✓" indicator via notification service or property
            // In the real app, this property exists on MainViewModel
            // We'll mock the notification for now
        }
    }
}