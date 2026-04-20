using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Presentation.ViewModels.GymHome
{
    public partial class ActiveMemberAvatarViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _initials = string.Empty;

        [ObservableProperty]
        private string _colorHex = "#3B82F6"; // Default Blue

        [ObservableProperty]
        private string _dimColorHex = "#203B82F6"; // Default Blue Dimmed
        
        [ObservableProperty]
        private int _overlapMargin;
    }
}
