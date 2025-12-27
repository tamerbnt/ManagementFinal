using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Services;
using Management.Application.Stores; // If you have RegistrationStore here
using Management.Domain.DTOs;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels
{
    public class RegistrationDetailViewModel : ViewModelBase, INavigationAware
    {
        private readonly IRegistrationService _registrationService;
        private readonly IDialogService _dialogService; // Used to close itself
        // private readonly RegistrationStore _registrationStore; // Optional if you need store updates

        private RegistrationDto _registration;
        public RegistrationDto Registration
        {
            get => _registration;
            set => SetProperty(ref _registration, value);
        }

        public ICommand CloseCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand DeclineCommand { get; }

        public RegistrationDetailViewModel(
            IRegistrationService registrationService,
            IDialogService dialogService)
        {
            _registrationService = registrationService;
            _dialogService = dialogService;

            // Close logic for a Modal is handled by the DialogService/Store generally, 
            // but often we want a specific Close button in the view.
            // If using ModalNavigationStore logic directly via DialogService, we might need a way to signal close.
            // For now, assuming DialogService might expose a Close method or we rely on overlay click.
            // A common pattern is injecting ModalNavigationStore directly or having DialogService.Close().
            CloseCommand = new RelayCommand(() =>
            {
                // Assuming you add a Close/Dismiss method to IDialogService or use the Store directly
                // _modalStore.Close(); 
            });

            ApproveCommand = new AsyncRelayCommand(ExecuteApprove);
            DeclineCommand = new AsyncRelayCommand(ExecuteDecline);
        }

        public async Task OnNavigatedToAsync(object parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is Guid id)
            {
                Registration = await _registrationService.GetRegistrationAsync(id);
            }
        }

        private async Task ExecuteApprove()
        {
            if (Registration == null) return;
            await _registrationService.ApproveRegistrationAsync(Registration.Id);
            // Close after action
        }

        private async Task ExecuteDecline()
        {
            if (Registration == null) return;
            await _registrationService.DeclineRegistrationAsync(Registration.Id);
            // Close after action
        }
    }
}