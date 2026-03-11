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
            try
            {
                await ExecuteSafeAsync(async () =>
                {
                    if (Appointment == null) return;

                    AppointmentStatus nextStatus = Appointment.Status switch
                    {
                        AppointmentStatus.Scheduled => AppointmentStatus.Confirmed,
                        AppointmentStatus.Confirmed => AppointmentStatus.InProgress,
                        AppointmentStatus.InProgress => AppointmentStatus.Completed,
                        _ => Appointment.Status
                    };

                    if (nextStatus != Appointment.Status)
                    {
                        // Capture data before VM is potentially disposed
                        var apptId = Appointment.Id;
                        var statusToSet = nextStatus;
                        var successMessageTemplate = _terminologyService.GetTerm("Terminology.Salon.AppointmentDetail.StatusUpdated");

                        // Await full modal closure (including animations) to ensure stable UI state
                        await _modalService.CloseCurrentModalAsync();

                        // Process update in background
                        _ = Task.Run(async () => 
                        {
                            try 
                            {
                                await _salonService.UpdateAppointmentStatusAsync(apptId, statusToSet);
                                
                                System.Windows.Application.Current?.Dispatcher.Invoke(() => 
                                {
                                    _notificationService.ShowSuccess(string.Format(successMessageTemplate, statusToSet));
                                });
                            }
                            catch (Exception)
                            {
                                _notificationService.ShowError("Failed to update appointment status in background");
                            }
                        });
                    }
                    else
                    {
                        await _modalService.CloseCurrentModalAsync();
                    }
                });
            }
            catch (Exception)
            {
                // Core exception handling
            }
        }
    }
}
