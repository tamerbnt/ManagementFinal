using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Presentation.ViewModels.GymHome
{
    public partial class GuideTipViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _stepNumber;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        public string StepBadge => $"STEP {StepNumber}";
    }
}
