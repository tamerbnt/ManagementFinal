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
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Domain.Enums;
using Management.Presentation.Messages;

namespace Management.Presentation.Views.Salon
{
    public class BookingViewModel : ViewModelBase
    {
        private readonly ISalonService _salonService;
        private readonly IMemberService _memberService;
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;
        private readonly IMembershipPlanService _planService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly ITerminologyService _terminologyService;
        private readonly Management.Domain.Services.ITenantService _tenantService;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<StaffMember> QualifiedStaff { get; } = new();
        public ObservableCollection<MemberDto> Clients { get; } = new();
        public ObservableCollection<SalonService> AvailableServices { get; } = new();

        private StaffMember? _selectedStaff;
        public StaffMember? SelectedStaff
        {
            get => _selectedStaff;
            set
            {
                if (SetProperty(ref _selectedStaff, value))
                {
                    SelectedStaffId = value?.Id ?? Guid.Empty;
                }
            }
        }

        private SalonService? _selectedService;
        public SalonService? SelectedService
        {
            get => _selectedService;
            set
            {
                if (SetProperty(ref _selectedService, value))
                {
                    SelectedPlanId = value?.Id;
                    if (value != null)
                    {
                        Price = value.BasePrice;
                    }
                }
            }
        }

        private Guid _selectedStaffId;
        public Guid SelectedStaffId
        {
            get => _selectedStaffId;
            set
            {
                if (SetProperty(ref _selectedStaffId, value))
                {
                    if (SelectedStaff?.Id != value)
                    {
                        SelectedStaff = QualifiedStaff.FirstOrDefault(s => s.Id == value);
                    }
                    ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
                }
            }
        }

        private Guid _selectedClientId;
        public Guid SelectedClientId
        {
            get => _selectedClientId;
            set
            {
        if (SetProperty(ref _selectedClientId, value))
        {
            var matchedClient = Clients.FirstOrDefault(c => c.Id == value);
            if (matchedClient != null)
            {
                _selectedClientName = matchedClient.FullName;
                OnPropertyChanged(nameof(SelectedClientName));
            }
            ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
        }
            }
        }

        private string _selectedClientName = string.Empty;
        public string SelectedClientName
        {
            get => _selectedClientName;
        set 
        {
            if (SetProperty(ref _selectedClientName, value))
            {
                // Try to find a matching client by name
                var match = Clients.FirstOrDefault(c => string.Equals(c.FullName, value, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (_selectedClientId != match.Id)
                    {
                        _selectedClientId = match.Id;
                        OnPropertyChanged(nameof(SelectedClientId));
                    }
                }
                else
                {
                    // If no match, reset ID but keep name (allows new client booking)
                    if (_selectedClientId != Guid.Empty)
                    {
                        _selectedClientId = Guid.Empty;
                        OnPropertyChanged(nameof(SelectedClientId));
                    }
                }
                ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
            }
        }
        }

        private Guid? _selectedPlanId;
        public Guid? SelectedPlanId
        {
            get => _selectedPlanId;
            set
            {
                if (SetProperty(ref _selectedPlanId, value))
                {
                    if (SelectedService?.Id != value)
                    {
                        SelectedService = AvailableServices.FirstOrDefault(s => s.Id == value);
                    }
                    ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
                }
            }
        }

        private string _selectedPlanName = string.Empty;

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

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set => SetProperty(ref _price, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand BookCommand => SaveCommand; // Alias for XAML
        public ICommand CancelCommand { get; }
        public ICommand AutoAssignCommand { get; }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            if (parameter is DateTime time)
            {
                BookingDate = time.Date;
                BookingTime = time.TimeOfDay;
            }
            else if (parameter is SalonBookArgs args)
            {
                BookingDate = args.Time.Date;
                BookingTime = args.Time.TimeOfDay;
                SelectedStaffId = args.StaffId;
            }

            await LoadInitialData();
        }
        public BookingViewModel(
            ISalonService salonService,
            IMemberService memberService,
            IMembershipPlanService planService,
            IModalNavigationService modalService,
            INotificationService notificationService, 
            Management.Domain.Services.IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            Management.Domain.Services.ITenantService tenantService)
        {
            _salonService = salonService;
            _memberService = memberService;
            _planService = planService;
            _modalService = modalService;
            _notificationService = notificationService;
            _facilityContext = facilityContext;
            _terminologyService = terminologyService;
            _tenantService = tenantService;

            // Use AsyncRelayCommand to properly propagate exceptions (avoid async void crash)
            SaveCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteSave, CanSave);
            CancelCommand = new Management.Presentation.Extensions.RelayCommand(() => _modalService.CloseModal());
            AutoAssignCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteAutoAssign);
        }

        private async Task LoadInitialData()
        {
            IsLoading = true;
            try
            {
                // Load Clients and Services
                var clientsTask = _memberService.SearchMembersAsync(_facilityContext.CurrentFacilityId, new MemberSearchRequest("", Management.Domain.Enums.MemberFilterType.All));
                var servicesInitTask = _salonService.LoadServicesAsync();
                var staffTask = _salonService.GetQualifiedStaffAsync(Guid.Empty);

                await Task.WhenAll(clientsTask, servicesInitTask, staffTask);

                var clientsResult = await clientsTask;
                if (clientsResult.IsSuccess)
                {
                    Clients.Clear();
                    foreach (var member in clientsResult.Value.Items) Clients.Add(member);
                }

                AvailableServices.Clear();
                foreach (var service in _salonService.Services) 
                {
                    AvailableServices.Add(service);
                }

                var staff = await staffTask;
                QualifiedStaff.Clear();
                foreach (var s in staff) QualifiedStaff.Add(s);
                
                // Re-sync selected items if IDs were already set (e.g. from SalonBookArgs)
                if (SelectedStaffId != Guid.Empty && SelectedStaff == null)
                {
                    SelectedStaff = QualifiedStaff.FirstOrDefault(s => s.Id == SelectedStaffId);
                }
            }
            catch (Exception)
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.ErrorLoading"), NotificationType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExecuteAutoAssign()
        {
            var startTime = BookingDate.Add(BookingTime);
            var staff = await _salonService.GetAutoAssignedStaffAsync(Guid.Empty, startTime);
            if (staff != null)
            {
                SelectedStaffId = staff.Id;
            }
            else
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.NoAvailableStaff"), NotificationType.Warning);
            }
        }

        private bool CanSave() => 
            !string.IsNullOrWhiteSpace(SelectedClientName) && 
            SelectedStaffId != Guid.Empty && 
            SelectedPlanId.HasValue &&
            BookingDate.Date >= DateTime.Today;

        private async Task ExecuteSave()
        {
            // Fix 4: Robust Validation Guards
            if (SelectedService == null || SelectedPlanId == null || SelectedPlanId == Guid.Empty)
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.ServiceRequired") ?? "Please select a service.", NotificationType.Error);
                return;
            }

            if (SelectedStaff == null || SelectedStaffId == Guid.Empty)
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.StaffRequired") ?? "Please select a staff member.", NotificationType.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedClientName))
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.ClientRequired") ?? "Please enter or select a client name.", NotificationType.Error);
                return;
            }

            if (BookingDate.Date < DateTime.Today)
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.DateInPast") ?? "Booking date cannot be in the past.", NotificationType.Error);
                return;
            }

            var startTime = BookingDate.Add(BookingTime);
            var endTime = startTime.AddMinutes(SelectedService.DurationMinutes > 0 ? SelectedService.DurationMinutes : 60);

            if (await _salonService.HasConflictAsync(SelectedStaffId, SelectedClientId, startTime, endTime))
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Conflict"), NotificationType.Error);
                return;
            }

            var clientId = SelectedClientId;
            
            // Auto-create client if it doesn't exist
            if (clientId == Guid.Empty && !string.IsNullOrWhiteSpace(SelectedClientName))
            {
                var newClient = new MemberDto
                {
                    FullName = SelectedClientName,
                    Status = MemberStatus.Active,
                    StartDate = DateTime.UtcNow,
                    ExpirationDate = DateTime.UtcNow.AddYears(1), // Default to 1 year for salon clients
                    Notes = "Auto-created from salon booking"
                };

                var createResult = await _memberService.CreateMemberAsync(_facilityContext.CurrentFacilityId, newClient);
                if (createResult.IsSuccess)
                {
                    clientId = createResult.Value;
                    // Notify Members list to refresh
                    WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<Member>(_facilityContext.CurrentFacilityId));
                }
                else
                {
                    _notificationService.ShowNotification("Failed to create client record, booking as guest.", NotificationType.Warning);
                }
            }

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantService.GetTenantId() ?? Guid.Empty,
                FacilityId = _facilityContext.CurrentFacilityId,
                ClientId = clientId,
                ClientName = SelectedClientName,
                StaffId = SelectedStaffId,
                StaffName = SelectedStaff.FullName, // Fix 2: Using property from SelectedStaff
                ServiceId = SelectedPlanId ?? Guid.Empty,
                ServiceName = SelectedService.Name, // Fix 1: Properly storing Service Name
                StartTime = startTime,
                EndTime = endTime,
                Price = Price,
                Status = AppointmentStatus.Scheduled,
                Notes = Notes
            };

            await _salonService.BookAppointmentAsync(appt);
            _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Success"), NotificationType.Success);
            _modalService.CloseModal();
        }
    }
}
