using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Stores;
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels.Shared
{
    public class ConfirmationModalConfig
    {
        public string Title { get; set; } = "Confirm Action";
        public string Message { get; set; } = "Are you sure you want to proceed?";
        public string ConfirmText { get; set; } = "Confirm";
        public string CancelText { get; set; } = "Cancel";
        public bool IsDestructive { get; set; } = false;
        public bool IsAlert { get; set; } = false;
        public bool IsSuccess { get; set; } = false;
    }

    public partial class ConfirmationModalViewModel : ObservableObject, IInitializable<object>, IModalResult<bool>
    {
        private readonly ModalNavigationStore _modalStore;
        private readonly IModalNavigationService _modalNavigationService;

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

        public bool Result { get; private set; }
        public bool HasResult { get; private set; }

        public IRelayCommand ConfirmCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public ConfirmationModalViewModel(
            ModalNavigationStore modalStore,
            IModalNavigationService modalNavigationService)
        {
            _modalStore = modalStore;
            _modalNavigationService = modalNavigationService;
            
            ConfirmCommand = new RelayCommand(async () => 
            {
                Result = true;
                HasResult = true;
                
                // 1. Close overlay if active
                await _modalStore.CloseAsync(ModalResult.Success());
                
                // 2. Close window if active
                if (_modalNavigationService.IsModalOpen && _modalNavigationService.CurrentModalViewModel == this)
                {
                    await _modalNavigationService.CloseCurrentModalAsync();
                }
            });

            CancelCommand = new RelayCommand(async () => 
            {
                Result = false;
                HasResult = true;

                // 1. Close overlay if active
                await _modalStore.CloseAsync(ModalResult.Cancel());

                // 2. Close window if active
                if (_modalNavigationService.IsModalOpen && _modalNavigationService.CurrentModalViewModel == this)
                {
                    await _modalNavigationService.CloseCurrentModalAsync();
                }
            });
        }

        public void Configure(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            IsDestructive = isDestructive;
            IsAlert = false;
        }

        public Task InitializeAsync(object parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is ConfirmationModalConfig config)
            {
                Title = config.Title;
                Message = config.Message;
                ConfirmText = config.ConfirmText;
                CancelText = config.CancelText;
                IsDestructive = config.IsDestructive;
                IsAlert = config.IsAlert;
                IsSuccess = config.IsSuccess;
            }
            return Task.CompletedTask;
        }
    }
}
