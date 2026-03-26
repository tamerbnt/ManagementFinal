using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.Models.Salon;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Domain.Services;

namespace Management.Presentation.Views.Salon
{
    public partial class AppointmentDetailViewModel : ViewModelBase
    {
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;
        private readonly Services.Salon.ISalonService _salonService;
        private readonly ITerminologyService _terminologyService;
        private readonly ILocalizationService _localizationService;

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
            ILocalizationService localizationService)
        {
            _modalService = modalService;
            _notificationService = notificationService;
            _salonService = salonService;
            _terminologyService = terminologyService;
            _localizationService = localizationService;
            CloseCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteClose);
            UpdateStatusCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteUpdateStatus);
        }

        public ICommand CloseCommand { get; }
        public ICommand UpdateStatusCommand { get; }

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
    }
}
