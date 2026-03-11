using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Stores;

namespace Management.Presentation.ViewModels.Shared
{
    public partial class ConfirmationModalViewModel : ObservableObject
    {
        private readonly ModalNavigationStore _modalStore;

        [ObservableProperty]
        private string _title = "Confirm Action";

        [ObservableProperty]
        private string _message = "Are you sure you want to proceed?";

        [ObservableProperty]
        private string _confirmText = "Confirm";

        [ObservableProperty]
        private string _cancelText = "Cancel";

        [ObservableProperty]
        private bool _isDestructive = false;

        [ObservableProperty]
        private bool _isAlert = false;

        [ObservableProperty]
        private bool _isSuccess = false;

        public IRelayCommand ConfirmCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public ConfirmationModalViewModel(ModalNavigationStore modalStore)
        {
            _modalStore = modalStore;
            
            ConfirmCommand = new RelayCommand(() => 
            {
                _ = _modalStore.CloseAsync(ModalResult.Success());
            });

            CancelCommand = new RelayCommand(() => 
            {
                _ = _modalStore.CloseAsync(ModalResult.Cancel());
            });
        }

        public void Configure(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            IsDestructive = isDestructive;
        }
    }
}
