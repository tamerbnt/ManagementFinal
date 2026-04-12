using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Services;
using Management.Presentation.Stores;
using Management.Domain.Services;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Auth
{
    public partial class LogoutConfirmationViewModel : ObservableObject, IModalViewModel, IModalResult<bool>
    {
        private readonly ModalNavigationStore _modalStore;
        private readonly IModalNavigationService _modalNavigationService;

        public bool Result { get; private set; }
        public bool HasResult { get; private set; }

        public ModalSize PreferredSize => ModalSize.Small;

        public IRelayCommand ConfirmCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public LogoutConfirmationViewModel(
            ModalNavigationStore modalStore,
            IModalNavigationService modalNavigationService)
        {
            _modalStore = modalStore;
            _modalNavigationService = modalNavigationService;

            ConfirmCommand = new AsyncRelayCommand(ExecuteConfirmAsync);
            CancelCommand = new AsyncRelayCommand(ExecuteCancelAsync);
        }

        private async Task ExecuteConfirmAsync()
        {
            Result = true;
            HasResult = true;
            await CloseAsync();
        }

        private async Task ExecuteCancelAsync()
        {
            Result = false;
            HasResult = true;
            await CloseAsync();
        }

        private async Task CloseAsync()
        {
            // Close overlay
            await _modalStore.CloseAsync(HasResult ? ModalResult.Success() : ModalResult.Cancel());

            // Close window if this is the current modal
            if (_modalNavigationService.IsModalOpen && _modalNavigationService.CurrentModalViewModel == this)
            {
                await _modalNavigationService.CloseCurrentModalAsync();
            }
        }

        public Task<bool> CanCloseAsync() => Task.FromResult(true);
    }
}
