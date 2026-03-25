using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Domain.Models.Salon;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Domain.Services;
using Management.Presentation.Services.Salon;
using Management.Presentation.ViewModels;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Messaging;
using Management.Domain.Services;
using Management.Presentation.Messages;

namespace Management.Presentation.Views.Salon
{
    public partial class AppointmentsViewModel : ViewModelBase, 
        Management.Application.Interfaces.ViewModels.INavigationalLifecycle,
        IRecipient<RefreshRequiredMessage<Appointment>>
    {
        private readonly ISalonService _salonService;
        private readonly INotificationService _notificationService;
        private readonly IModalNavigationService _modalService;
        private readonly IStaffService _staffService;
        private readonly ITerminologyService _terminologyService;
        private readonly ILocalizationService _localizationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;


        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set 
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    OnPropertyChanged(nameof(SelectedDateDisplay));
                    _ = LoadAppointments();
                }
            }
        }

        [ObservableProperty]
        private bool _isCompactMode;

        public ObservableCollection<TimeSlotViewModel> TimeSlots { get; } = new();
        public ObservableCollection<AppointmentViewModel> Appointments { get; } = new();
        public ObservableCollection<SchedulerStaffViewModel> Stylists { get; } = new();

        public string SelectedDateDisplay => SelectedDate.ToString(_terminologyService.GetTerm("Terminology.Salon.Appointments.DateDisplayFormat"), _localizationService.CurrentCulture);
        public string BookingsTodayLabel => _terminologyService.GetTerm("Terminology.Salon.Appointments.BookingsToday");
        public string TodayLabel => _terminologyService.GetTerm("Terminology.Salon.Appointments.Today");

        public ICommand ToggleDensityCommand { get; }
        public ICommand ViewDetailCommand { get; }
        public ICommand BookCommand { get; }
        public ICommand RescheduleCommand { get; }
        public ICommand NextDayCommand { get; }
        public ICommand PrevDayCommand { get; }
        public ICommand TodayCommand { get; }
        public ICommand AddStaffCommand { get; }
        public AppointmentsViewModel(
            ISalonService salonService,
            IStaffService staffService,
            INotificationService notificationService, 
            IModalNavigationService modalService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _salonService = salonService;
            _staffService = staffService;
            _notificationService = notificationService;
            _modalService = modalService;
            _terminologyService = terminologyService;
            _localizationService = localizationService;
            _facilityContext = facilityContext;

            ToggleDensityCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => IsCompactMode = !IsCompactMode);
            ViewDetailCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<AppointmentViewModel>(async (appt) => await OpenDetail(appt));
            BookCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<SalonBookArgs>(async (args) => await OpenBooking(args));
            RescheduleCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<AppointmentRescheduleArgs>(async (args) => await ExecuteReschedule(args));
            AddStaffCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(async () => await ExecuteAddStaff());
            
            NextDayCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => SelectedDate = SelectedDate.AddDays(1));
            PrevDayCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            TodayCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => SelectedDate = DateTime.Today);

            InitializeCalendar();
            // InitializeAsync() is removed, loading will happen via LoadDeferredAsync

            // Subscribe to real-time status changes from the service layer
            _salonService.AppointmentStatusChanged += OnAppointmentStatusChanged;
            _salonService.AppointmentAdded += OnAppointmentAdded;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public void Receive(RefreshRequiredMessage<Appointment> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            
            _logger?.LogInformation("[Appointments] Refresh message received for facility {Id}", message.Value);
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () => await LoadAppointments());
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task PreInitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            await LoadInitialData();
        }

        private void OnAppointmentStatusChanged(object? sender, (Guid AppointmentId, AppointmentStatus NewStatus) e)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
            {
                var wrapper = Appointments.FirstOrDefault(a => a.Id == e.AppointmentId);
                if (wrapper != null)
                {
                    wrapper.Status = e.NewStatus;
                }
            });
        }

        private void OnAppointmentAdded(object? sender, Appointment appt)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                // Trigger a full reload if the appointment belongs to the displayed date
                // This ensures AssignLanes handles overlap correctly, and horizontal MultiBinding regenerates.
                if (appt.StartTime.Date == SelectedDate.Date)
                {
                    await LoadAppointments();
                }
            });
        }


        private async Task LoadInitialData()
        {
            try
            {
                await Task.WhenAll(LoadStylistsAsync(), LoadAppointments());
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Loading Failed", "Could not load schedule data.");
            }
        }

        private async Task LoadStylistsAsync()
        {
            var result = await _staffService.GetAllStaffAsync();
            if (result.IsSuccess)
            {
                Stylists.Clear();
                foreach (var dto in result.Value)
                {
                    Stylists.Add(new SchedulerStaffViewModel 
                    { 
                        Id = dto.Id, 
                        FullName = dto.FullName,
                        Role = ((Management.Domain.Enums.StaffRole)dto.Role).ToString()
                    });
                }
            }
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

        private async Task LoadAppointments()
        {
            IsLoading = true;
            try
            {
                await _salonService.LoadAppointmentsAsync(_facilityContext.CurrentFacilityId, SelectedDate);
                
                Appointments.Clear();
                var wrappers = _salonService.Appointments
                    .Where(a => a != null)
                    .Select(a => new AppointmentViewModel(a, Stylists.ToList(), IsCompactMode))
                    .ToList();

                AssignLanes(wrappers);

                foreach (var w in wrappers)
                    Appointments.Add(w);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError(_terminologyService.GetTerm("Terminology.Home.Status.Warning"), _terminologyService.GetTerm("Terminology.Salon.Errors.LoadAppointments"));
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Lane-splitting algorithm (Google Calendar style).
        /// Groups appointments by stylist column, finds overlap clusters,
        /// and assigns each card a LaneIndex + LaneCount so they split
        /// the column width instead of stacking.
        /// </summary>
        private static void AssignLanes(List<AppointmentViewModel> wrappers)
        {
            // Process each stylist column independently
            var byColumn = wrappers.GroupBy(w => w.ColumnIndex);

            foreach (var column in byColumn)
            {
                var sorted = column.OrderBy(a => a.StartTime).ThenBy(a => a.EndTime).ToList();

                // lanes[i] = end time of the last appointment placed in lane i
                var laneEndTimes = new List<DateTime>();

                foreach (var appt in sorted)
                {
                    // Find the first lane whose last appointment has already ended
                    int assignedLane = -1;
                    for (int i = 0; i < laneEndTimes.Count; i++)
                    {
                        if (laneEndTimes[i] <= appt.StartTime)
                        {
                            assignedLane = i;
                            laneEndTimes[i] = appt.EndTime;
                            break;
                        }
                    }

                    if (assignedLane == -1)
                    {
                        // No free lane — open a new one
                        assignedLane = laneEndTimes.Count;
                        laneEndTimes.Add(appt.EndTime);
                    }

                    appt.LaneIndex = assignedLane;
                }

                int totalLanes = laneEndTimes.Count;

                // Now we need to set LaneCount for each appointment.
                // An appointment's LaneCount = the max number of concurrent lanes
                // in its time window (so it fills the right fraction of the column).
                // Simple approach: LaneCount = totalLanes for the whole column.
                // This is correct for dense columns; for sparse ones it's slightly
                // conservative but always safe (no overlap).
                foreach (var appt in sorted)
                    appt.LaneCount = totalLanes;
            }
        }

        private async Task OpenDetail(AppointmentViewModel appt)
        {
            if (appt == null) return;
            
            var model = _salonService.Appointments.FirstOrDefault(a => a.Id == appt.Id);
            if (model != null)
            {
                await _modalService.OpenModalAsync<AppointmentDetailViewModel>(parameter: model);
            }
        }

        private async Task OpenBooking(SalonBookArgs args)
        {
            if (args == null) return;
            
            try
            {
                // Open modal and pass the args
                await _modalService.OpenModalAsync<BookingViewModel>(parameter: args);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError(_terminologyService.GetTerm("Terminology.Home.Status.Warning"), _terminologyService.GetTerm("Terminology.Salon.Errors.OpenBooking"));
            }
        }

        private async Task ExecuteReschedule(AppointmentRescheduleArgs args)
        {
            var appt = _salonService.Appointments.FirstOrDefault(a => a.Id == args.Id);
            if (appt == null) return;

            var oldStart = appt.StartTime;
            await _salonService.RescheduleAppointmentAsync(args.Id, args.NewStart);
            
            _notificationService.ShowSuccess(
                string.Format(_terminologyService.GetTerm("Terminology.Salon.Appointments.MoveSuccess"), args.NewStart.ToString("t", _localizationService.CurrentCulture)),
                async () => { 
                    await _salonService.RescheduleAppointmentAsync(args.Id, oldStart);
                    await LoadAppointments();
                });
            
            await LoadAppointments();
        }

        private async Task ExecuteAddStaff()
        {
            var currentIds = Stylists.Select(s => s.Id).ToList();
            var args = new SalonAddStaffArgs(currentIds, async (selectedStaff) => 
            {
                if (selectedStaff != null && !Stylists.Any(s => s.Id == selectedStaff.Id))
                {
                    Stylists.Add(new SchedulerStaffViewModel 
                    { 
                        Id = selectedStaff.Id, 
                        FullName = selectedStaff.FullName,
                        Role = selectedStaff.Role.ToString()
                    });
                    await LoadAppointments(); // Refresh to show appointments for new staff
                }
            });

            await _modalService.OpenModalAsync<SalonAddStaffViewModel>(parameter: args);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _salonService.AppointmentStatusChanged -= OnAppointmentStatusChanged;
                _salonService.AppointmentAdded -= OnAppointmentAdded;
            }
            base.Dispose(disposing);
        }
    }

    public class TimeSlotViewModel : ViewModelBase
    {
        public DateTime Time { get; set; }
        public string label => Time.ToString("HH:mm");
        public bool IsHourMark => Time.Minute == 0;
    }

    public partial class AppointmentViewModel : ViewModelBase
    {
        private readonly Appointment _model;
        private readonly List<SchedulerStaffViewModel> _allStylists;
        private readonly bool _isCompact;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusBackgroundBrush))]
        [NotifyPropertyChangedFor(nameof(StatusAccentBrush))]
        [NotifyPropertyChangedFor(nameof(ServiceTextBrush))]
        private AppointmentStatus _status;

        [ObservableProperty]
        private bool _isUpdating;

        public AppointmentViewModel(Appointment model, List<SchedulerStaffViewModel> allStylists, bool isCompact = false)
        {
            _model = model;
            _allStylists = allStylists;
            _isCompact = isCompact;
            _status = model.Status;

            // Cache performance-critical calculations once during initialization
            double factor = _isCompact ? (20.0 / 15.0) : (40.0 / 15.0);
            TopOffset = Math.Max(0, (StartTime.TimeOfDay - TimeSpan.FromHours(8)).TotalMinutes * factor);
            Height = Math.Max(factor * 15, (EndTime - StartTime).TotalMinutes * factor);
            ColumnIndex = Math.Max(0, _allStylists.FindIndex(s => s.Id == StaffId || (!string.IsNullOrEmpty(StaffName) && s.FullName == StaffName)));
            ColumnLeftPosition = ColumnIndex * 250.0;
        }

        public Guid Id => _model.Id;
        public string ClientName => _model.ClientName;
        public string ServiceName => _model.ServiceName;
        public string StaffName => _model.StaffName;
        public Guid StaffId => _model.StaffId;
        public DateTime StartTime => _model.StartTime;
        public DateTime EndTime => _model.EndTime;

        public double TopOffset { get; }
        public double Height { get; }
        public int ColumnIndex { get; }
        public double ColumnLeftPosition { get; }
        public double ColumnWidth { get; set; } 

        // Lane-splitting: set by the lane assignment algorithm in AppointmentsViewModel
        public int LaneIndex { get; set; } = 0;
        public int LaneCount { get; set; } = 1;

        // Optimized UI Brushes to avoid DataTrigger overhead
        public Brush StatusBackgroundBrush => GetResourceBrush(Status switch
        {
            AppointmentStatus.Scheduled => "StatusRoseBackground",
            AppointmentStatus.Confirmed => "StatusInfoBackground",
            AppointmentStatus.InProgress => "StatusWarningBackground",
            AppointmentStatus.Completed => "StatusSuccessBackground",
            AppointmentStatus.NoShow => "StatusErrorBackground",
            _ => "StatusRoseBackground"
        });

        public Brush StatusAccentBrush => GetResourceBrush(Status switch
        {
            AppointmentStatus.Scheduled => "FacilityAccentBrush",
            AppointmentStatus.Confirmed => "StatusInfoBrush",
            AppointmentStatus.InProgress => "StatusWarningBrush",
            AppointmentStatus.Completed => "StatusSuccessBrush",
            AppointmentStatus.NoShow => "StatusErrorBrush",
            _ => "FacilityAccentBrush"
        });

        public Brush ServiceTextBrush => GetResourceBrush(Status switch
        {
            AppointmentStatus.Confirmed => "StatusInfoBrush",
            AppointmentStatus.InProgress => "StatusWarningBrush",
            AppointmentStatus.Completed => "StatusSuccessBrush",
            AppointmentStatus.NoShow => "StatusErrorBrush",
            _ => "FacilityAccentBrush"
        });

        private Brush GetResourceBrush(string resourceKey)
        {
            return System.Windows.Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
        }
    }

    public class AppointmentRescheduleArgs
    {
        public Guid Id { get; set; }
        public DateTime NewStart { get; set; }
    }

    public class SchedulerStaffViewModel 
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
    }
    public class SalonBookArgs
    {
        public DateTime Time { get; set; }
        public Guid StaffId { get; set; }
    }
}
