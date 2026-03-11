using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class DayScheduleViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _dayName = string.Empty;

        [ObservableProperty]
        private string _openTime = "08:00";

        [ObservableProperty]
        private string _closeTime = "22:00";

        [ObservableProperty]
        private bool _isOpen = true;
    }

    public partial class ZoneViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private int _capacity;
    }
}
