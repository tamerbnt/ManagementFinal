using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class ScheduleWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _dayOfWeek;

        [ObservableProperty]
        private string _startTime = "09:00";

        [ObservableProperty]
        private string _endTime = "21:00";

        public string DayName => DayOfWeek switch
        {
            0 => "Sunday",
            1 => "Monday",
            2 => "Tuesday",
            3 => "Wednesday",
            4 => "Thursday",
            5 => "Friday",
            6 => "Saturday",
            _ => "Unknown"
        };
    }
}
