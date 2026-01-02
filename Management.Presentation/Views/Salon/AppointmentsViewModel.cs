using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Management.Domain.Models.Salon;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Salon;

namespace Management.Presentation.Views.Salon
{
    public class AppointmentsViewModel : ViewModelBase
    {
        private readonly ISalonService _salonService;
        private readonly INotificationService _notificationService;
        private readonly IModalNavigationService _modalService;

        private bool _isCompactMode;
        public bool IsCompactMode
        {
            get => _isCompactMode;
            set => SetProperty(ref _isCompactMode, value);
        }

        public ObservableCollection<TimeSlotViewModel> TimeSlots { get; } = new();
        public ObservableCollection<AppointmentViewModel> Appointments { get; } = new();

        public ICommand ToggleDensityCommand { get; }
        public ICommand BookCommand { get; }
        public ICommand RescheduleCommand { get; }

        public AppointmentsViewModel(ISalonService salonService, INotificationService notificationService, IModalNavigationService modalService)
        {
            _salonService = salonService;
            _notificationService = notificationService;
            _modalService = modalService;

            ToggleDensityCommand = new RelayCommand(() => IsCompactMode = !IsCompactMode);
            BookCommand = new RelayCommand<DateTime>(async (time) => await OpenBooking(time));
            RescheduleCommand = new RelayCommand<AppointmentRescheduleArgs>(async (args) => await ExecuteReschedule(args));

            InitializeCalendar();
            LoadAppointments();
        }

        private void InitializeCalendar()
        {
            TimeSlots.Clear();
            var start = DateTime.Today.AddHours(8);
            var end = DateTime.Today.AddHours(20);

            while (start <= end)
            {
                TimeSlots.Add(new TimeSlotViewModel { Time = start });
                start = start.AddMinutes(15);
            }
        }

        private void LoadAppointments()
        {
            foreach (var appt in _salonService.Appointments)
            {
                Appointments.Add(new AppointmentViewModel(appt));
            }
        }

        private async Task OpenBooking(DateTime time)
        {
            await _modalService.OpenModalAsync<BookingViewModel>(parameter: time);
        }

        private async Task ExecuteReschedule(AppointmentRescheduleArgs args)
        {
            var appt = _salonService.Appointments.FirstOrDefault(a => a.Id == args.Id);
            if (appt == null) return;

            var oldStart = appt.StartTime;
            await _salonService.RescheduleAppointmentAsync(args.Id, args.NewStart);
            
            _notificationService.ShowUndoNotification(
                $"Appointment moved to {args.NewStart:t}",
                async () => { 
                    await _salonService.RescheduleAppointmentAsync(args.Id, oldStart);
                    return;
                },
                () => Task.CompletedTask);
        }
    }

    public class TimeSlotViewModel : ViewModelBase
    {
        public DateTime Time { get; set; }
        public string label => Time.ToString("HH:mm");
        public bool IsHourMark => Time.Minute == 0;
    }

    public class AppointmentViewModel : ViewModelBase
    {
        private readonly Appointment _model;
        public AppointmentViewModel(Appointment model) => _model = model;

        public Guid Id => _model.Id;
        public string ClientName => _model.ClientName;
        public string ServiceName => _model.ServiceName;
        public string StaffName => _model.StaffName;
        public DateTime StartTime => _model.StartTime;
        public DateTime EndTime => _model.EndTime;
        public AppointmentStatus Status => _model.Status;

        // Position helper properties for XAML
        public double TopOffset => Math.Max(0, (StartTime - DateTime.Today.AddHours(8)).TotalMinutes * 2); // 2px per minute
        public double Height => (EndTime - StartTime).TotalMinutes * 2;
    }

    public class AppointmentRescheduleArgs
    {
        public Guid Id { get; set; }
        public DateTime NewStart { get; set; }
    }
}
