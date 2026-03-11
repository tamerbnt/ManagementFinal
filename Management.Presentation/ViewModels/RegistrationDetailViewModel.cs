using System;
using Management.Application.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.DTOs;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels
{
    public class RegistrationDetailViewModel : ViewModelBase, INavigationAware
    {
        private readonly IRegistrationService _registrationService;
        private readonly IDialogService _dialogService;
        private readonly IFacilityContextService _facilityContext;

        private RegistrationDto _registration = null!;
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
            IDialogService dialogService,
            IFacilityContextService facilityContext)
        {
            _registrationService = registrationService;
            _dialogService = dialogService;
            _facilityContext = facilityContext;

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

        public async Task OnNavigatedTo(object parameter)
        {
            if (parameter is Guid id)
            {
                var result = await _registrationService.GetRegistrationAsync(_facilityContext.CurrentFacilityId, id);
                if (result.IsSuccess)
                {
                    Registration = result.Value;
                }
            }
        }

        public Task OnNavigatedFrom()
        {
            return Task.CompletedTask;
        }

        private async Task ExecuteApprove()
        {
            if (Registration == null) return;
            var result = await _registrationService.ApproveRegistrationAsync(_facilityContext.CurrentFacilityId, Registration.Id);
            if (result.IsSuccess)
            {
                // Signify success/close
            }
        }

        private async Task ExecuteDecline()
        {
            if (Registration == null) return;
            var result = await _registrationService.DeclineRegistrationAsync(_facilityContext.CurrentFacilityId, Registration.Id);
            if (result.IsSuccess)
            {
                // Signify success/close
            }
        }
    }
}
