using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.Models.Salon;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Domain.Services;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;

namespace Management.Presentation.Views.Salon
{
    public partial class AppointmentDetailViewModel : ViewModelBase
    {
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;
        private readonly Services.Salon.ISalonService _salonService;
        private readonly ITerminologyService _terminologyService;
        private readonly ILocalizationService _localizationService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActionButtonText))]
        private Appointment _appointment;

        public string ActionButtonText => Appointment?.Status switch
        {
            AppointmentStatus.Scheduled => _terminologyService.GetTerm("Terminology.Salon.AppointmentDetail.Action.Confirm"),
            AppointmentStatus.Confirmed => _terminologyService.GetTerm("Terminology.Salon.AppointmentDetail.Action.Start"),
            AppointmentStatus.InProgress => _terminologyService.GetTerm("Terminology.Salon.AppointmentDetail.Action.Complete"),
            _ => Appointment?.Status.ToString() ?? _terminologyService.GetTerm("Terminology.Global.Close")
        };
        public AppointmentDetailViewModel(
            IModalNavigationService modalService, 
            INotificationService notificationService,
            Services.Salon.ISalonService salonService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            IDialogService dialogService)
        {
            _modalService = modalService;
            _notificationService = notificationService;
            _salonService = salonService;
            _terminologyService = terminologyService;
            _localizationService = localizationService;
            _dialogService = dialogService;
            CloseCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteClose);
            UpdateStatusCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteUpdateStatus);
            CancelCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteCancel);
        }

        public ICommand CloseCommand { get; }
        public ICommand UpdateStatusCommand { get; }
        public ICommand CancelCommand { get; }

        public override System.Threading.Tasks.Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            if (parameter is Appointment appointment)
            {
                Appointment = appointment;
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void ExecuteClose()
        {
            _modalService.CloseModal();
        }

        private async System.Threading.Tasks.Task ExecuteUpdateStatus()
        {
            await ExecuteSafeAsync(async () =>
            {
                if (Appointment == null) return;

                AppointmentStatus nextStatus = Appointment.Status switch
                {
                    AppointmentStatus.Scheduled  => AppointmentStatus.Confirmed,
                    AppointmentStatus.Confirmed  => AppointmentStatus.InProgress,
                    AppointmentStatus.InProgress => AppointmentStatus.Completed,
                    _                            => Appointment.Status
                };

                if (nextStatus == Appointment.Status)
                {
                    await _modalService.CloseCurrentModalAsync();
                    return;
                }

                // Step 1 — update the database first
                await _salonService.UpdateAppointmentStatusAsync(Appointment.Id, nextStatus);

                // Step 2 — close modal only after successful update
                await _modalService.CloseCurrentModalAsync();

                // Step 3 — show success notification
                _notificationService.ShowSuccess(
                    $"Appointment moved to {nextStatus}");
            });
        }

        private async System.Threading.Tasks.Task ExecuteCancel()
        {
            if (Appointment == null) return;

            // Atomic Pattern: Delete -> Save (Service handles) -> Notify with Undo
            var appointmentId = Appointment.Id;
            var clientName = Appointment.ClientName;

            await _salonService.CancelAppointmentAsync(appointmentId);

            // Close modal immediately
            await _modalService.CloseCurrentModalAsync();

            // Notify with Undo
            _notificationService.ShowSuccess(
                $"Appointment for {clientName} cancelled.",
                undoAction: async () => 
                {
                    await _salonService.RestoreAppointmentAsync(appointmentId);
                    // Force refresh to ensure schedule updates
                    WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<Appointment>(Appointment.FacilityId));
                });
        }
    }
}
