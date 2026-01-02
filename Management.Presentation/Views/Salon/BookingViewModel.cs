using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Domain.Models.Salon;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Salon;

namespace Management.Presentation.Views.Salon
{
    public class BookingViewModel : ViewModelBase
    {
        private readonly ISalonService _salonService;
        private readonly IMemberService _memberService;
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;

        private Guid _selectedServiceId;
        public Guid SelectedServiceId
        {
            get => _selectedServiceId;
            set
            {
                if (SetProperty(ref _selectedServiceId, value))
                {
                    LoadQualifiedStaff();
                }
            }
        }

        private Guid _selectedStaffId;
        public Guid SelectedStaffId
        {
            get => _selectedStaffId;
            set => SetProperty(ref _selectedStaffId, value);
        }

        private Guid _selectedClientId;
        public Guid SelectedClientId
        {
            get => _selectedClientId;
            set => SetProperty(ref _selectedClientId, value);
        }

        private DateTime _bookingDate = DateTime.Today;
        public DateTime BookingDate
        {
            get => _bookingDate;
            set => SetProperty(ref _bookingDate, value);
        }

        private TimeSpan _bookingTime;
        public TimeSpan BookingTime
        {
            get => _bookingTime;
            set => SetProperty(ref _bookingTime, value);
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public ObservableCollection<SalonService> Services => _salonService.Services;
        public ObservableCollection<StaffMember> QualifiedStaff { get; } = new();
        public ObservableCollection<MemberDto> Clients { get; } = new();

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AutoAssignCommand { get; }

        public BookingViewModel(
            ISalonService salonService, 
            IMemberService memberService,
            IModalNavigationService modalService,
            INotificationService notificationService,
            Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _salonService = salonService;
            _memberService = memberService;
            _modalService = modalService;
            _notificationService = notificationService;
            _facilityContext = facilityContext;

            SaveCommand = new RelayCommand(async () => await ExecuteSave(), CanSave);
            CancelCommand = new RelayCommand(() => _modalService.CloseModal());
            AutoAssignCommand = new RelayCommand(async () => await ExecuteAutoAssign());

            LoadInitialData();
        }

        private async void LoadInitialData()
        {
            var request = new MemberSearchRequest("", Management.Domain.Enums.MemberFilterType.All);
            var result = await _memberService.SearchMembersAsync(_facilityContext.CurrentFacilityId, request);
            if (result.IsSuccess)
            {
                foreach (var member in result.Value.Items) Clients.Add(member);
            }
        }

        private async void LoadQualifiedStaff()
        {
            QualifiedStaff.Clear();
            var staff = await _salonService.GetQualifiedStaffAsync(SelectedServiceId);
            foreach (var s in staff) QualifiedStaff.Add(s);
        }

        private async Task ExecuteAutoAssign()
        {
            var startTime = BookingDate.Add(BookingTime);
            var staff = await _salonService.GetAutoAssignedStaffAsync(SelectedServiceId, startTime);
            if (staff != null)
            {
                SelectedStaffId = staff.Id;
            }
            else
            {
                _notificationService.ShowNotification("No available staff for this time", NotificationType.Warning);
            }
        }

        private bool CanSave() => SelectedServiceId != Guid.Empty && SelectedClientId != Guid.Empty && SelectedStaffId != Guid.Empty;

        private async Task ExecuteSave()
        {
            var service = Services.First(s => s.Id == SelectedServiceId);
            var startTime = BookingDate.Add(BookingTime);
            var endTime = startTime.AddMinutes(service.DurationMinutes);

            if (await _salonService.HasConflictAsync(SelectedStaffId, SelectedClientId, startTime, endTime))
            {
                _notificationService.ShowNotification("Schedule conflict detected!", NotificationType.Error);
                return;
            }

            var appt = new Appointment
            {
                ClientId = SelectedClientId,
                ClientName = Clients.First(c => c.Id == SelectedClientId).FullName,
                StaffId = SelectedStaffId,
                StaffName = QualifiedStaff.First(s => s.Id == SelectedStaffId).FullName,
                ServiceId = SelectedServiceId,
                ServiceName = service.Name,
                StartTime = startTime,
                EndTime = endTime,
                Status = AppointmentStatus.Scheduled,
                Notes = Notes
            };

            _salonService.Appointments.Add(appt);
            _notificationService.ShowNotification("Appointment booked successfully", NotificationType.Success);
            _modalService.CloseModal();
        }
    }
}
