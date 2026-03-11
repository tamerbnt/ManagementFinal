using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Sync
{
    public partial class ConflictResolutionViewModel : ObservableObject
    {
        // In a real scenario, these would be typed objects or a diff structure.
        // For visual simplicity, we'll use strings or a specific model wrapper.
        
        [ObservableProperty]
        private string _entityName;

        [ObservableProperty]
        private string _conflictDescription;

        [ObservableProperty]
        private string _localVersionDetails;

        [ObservableProperty]
        private string _serverVersionDetails;

        public bool? Result { get; private set; } // true = Keep Local, false = Keep Server, null = Cancel

        public ConflictResolutionViewModel()
        {
            // Design-time data
            EntityName = "Member Profile: John Doe";
            ConflictDescription = "This record was modified on another device.";
            LocalVersionDetails = "Email: john@example.com\nPhone: 555-0100\nLast Modified: Just now";
            ServerVersionDetails = "Email: john.doe@example.com\nPhone: 555-0100\nLast Modified: 5 mins ago";
        }

        public void Initialize(string entityName, string localDetails, string serverDetails)
        {
            EntityName = entityName;
            LocalVersionDetails = localDetails;
            ServerVersionDetails = serverDetails;
        }

        [RelayCommand]
        private void KeepLocal()
        {
            Result = true;
            // Close dialog logic (usually handled by View or DialogService binding)
            OnRequestClose();
        }

        [RelayCommand]
        private void KeepServer()
        {
            Result = false;
            OnRequestClose();
        }

        public event System.Action RequestClose;

        private void OnRequestClose()
        {
            RequestClose?.Invoke();
        }
    }
}
