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
using MediatR;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Domain.Enums;
using Management.Presentation.Messages;

namespace Management.Presentation.Views.Salon
{
    public partial class BookingViewModel : ViewModelBase
    {
        private readonly ISalonService _salonService;
        private readonly IMemberService _memberService;
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;
        private readonly IMembershipPlanService _planService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly ITerminologyService _terminologyService;
        private readonly Management.Domain.Services.ITenantService _tenantService;
        private readonly IMediator _mediator;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<StaffMember> QualifiedStaff { get; } = new();
        public ObservableCollection<MemberDto> Clients { get; } = new();
        public ObservableCollection<SalonService> AvailableServices { get; } = new();
        public ObservableCollection<string> AcquisitionSources { get; } = new() { "Walk-in", "Word of Mouth", "Instagram", "TikTok", "Facebook" };
        public ObservableCollection<MemberDto> FilteredClients { get; } = new();

        [ObservableProperty]
        private bool _isExistingClientMode = true;

        partial void OnIsExistingClientModeChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowSearchPanel));
        }

        [ObservableProperty]
        private string _clientSearchText = string.Empty;

        partial void OnClientSearchTextChanged(string value)
        {
            FilterClients(value);
        }

        private void FilterClients(string query)
        {
            FilteredClients.Clear();
            if (string.IsNullOrWhiteSpace(query)) return;

            var results = Clients
                .Where(c => c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(5);

            foreach (var client in results)
                FilteredClients.Add(client);
        }

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
                    SelectedServiceId = value?.Id;
                    UpdatePrice();
                }
            }
        }

        private int? _age;
        public int? Age
        {
            get => _age;
            set => SetProperty(ref _age, value);
        }

        private string _acquisitionSource = "Walk-in";
        public string AcquisitionSource
        {
            get => _acquisitionSource;
            set => SetProperty(ref _acquisitionSource, value);
        }

        private void UpdatePrice()
        {
            decimal total = 0;
            if (SelectedService != null)
                total += SelectedService.BasePrice;
            
            Price = total;
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
            {
                ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
            });
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
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
                    {
                        ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
                    });
                }
            }
        }

        private MemberDto? _selectedClient;
        public MemberDto? SelectedClient
        {
            get => _selectedClient;
            set
            {
                if (SetProperty(ref _selectedClient, value))
                {
                    OnPropertyChanged(nameof(HasSelectedClient));
                    OnPropertyChanged(nameof(ShowSearchPanel));
                }
            }
        }

        public bool HasSelectedClient => SelectedClient != null;
        public bool ShowSearchPanel => !HasSelectedClient && IsExistingClientMode;

        private Guid _selectedClientId;
        public Guid SelectedClientId
        {
            get => _selectedClientId;
            set
            {
                if (SetProperty(ref _selectedClientId, value))
                {
                    SelectedClient = Clients.FirstOrDefault(c => c.Id == value);
                    if (SelectedClient != null)
                    {
                        _selectedClientName = SelectedClient.FullName;
                        OnPropertyChanged(nameof(SelectedClientName));
                    }
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
                    {
                        ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
                    });
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
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
                    {
                        ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
                    });
                }
            }
        }

        private Guid? _selectedServiceId;
        public Guid? SelectedServiceId
        {
            get => _selectedServiceId;
            set
            {
                if (SetProperty(ref _selectedServiceId, value))
                {
                    if (SelectedService?.Id != value)
                    {
                        SelectedService = AvailableServices.FirstOrDefault(s => s.Id == value);
                    }
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
                    {
                        ((CommunityToolkit.Mvvm.Input.AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
                    });
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

        public IRelayCommand ClearSelectedClientCommand { get; }
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
            Management.Domain.Services.ITenantService tenantService,
            IMediator mediator)
        {
            _salonService = salonService;
            _memberService = memberService;
            _planService = planService;
            _modalService = modalService;
            _notificationService = notificationService;
            _facilityContext = facilityContext;
            _terminologyService = terminologyService;
            _tenantService = tenantService;
            _mediator = mediator;

            // Use AsyncRelayCommand to properly propagate exceptions (avoid async void crash)
            SaveCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteSave, CanSave);
            CancelCommand = new Management.Presentation.Extensions.RelayCommand(() => _modalService.CloseModal());
            AutoAssignCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteAutoAssign);
            ClearSelectedClientCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteClearSelectedClient);
        }

        private void ExecuteClearSelectedClient()
        {
            SelectedClientId = Guid.Empty;
            SelectedClientName = string.Empty;
            SelectedClient = null;
            ClientSearchText = string.Empty;
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
            (IsExistingClientMode ? SelectedClientId != Guid.Empty : !string.IsNullOrWhiteSpace(SelectedClientName)) && 
            SelectedStaffId != Guid.Empty && 
            SelectedService != null &&
            BookingDate.Date >= DateTime.Today;

        private async Task ExecuteSave()
        {
            if (SelectedService == null)
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.Required") ?? "Please select a service.", NotificationType.Error);
                return;
            }

            if (SelectedStaff == null || SelectedStaffId == Guid.Empty)
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.StaffRequired") ?? "Please select a staff member.", NotificationType.Error);
                return;
            }

            if (!IsExistingClientMode && string.IsNullOrWhiteSpace(SelectedClientName))
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.ClientRequired") ?? "Please enter a client name.", NotificationType.Error);
                return;
            }

            if (IsExistingClientMode && SelectedClientId == Guid.Empty)
            {
                _notificationService.ShowNotification("Please select an existing client.", NotificationType.Error);
                return;
            }

            if (BookingDate.Date < DateTime.Today)
            {
                _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Validation.DateInPast") ?? "Booking date cannot be in the past.", NotificationType.Error);
                return;
            }

            try 
            {
                var startTime = BookingDate.Date.Add(BookingTime);
                var duration = SelectedService.DurationMinutes > 0 ? SelectedService.DurationMinutes : 60;
                var endTime = startTime.AddMinutes(duration);

                if (await _salonService.HasConflictAsync(SelectedStaffId, SelectedClientId, startTime, endTime))
                {
                    _notificationService.ShowNotification(_terminologyService.GetTerm("Terminology.Salon.Booking.Conflict") ?? "Schedule conflict detected.", NotificationType.Error);
                    return;
                }

                var clientId = SelectedClientId;
                var clientName = SelectedClientName;
                
                if (clientId == Guid.Empty && !IsExistingClientMode)
                {
                    var newClient = new MemberDto
                    {
                        FullName = SelectedClientName,
                        Status = MemberStatus.Active,
                        StartDate = DateTime.UtcNow,
                        ExpirationDate = DateTime.UtcNow.AddYears(1),
                        DateOfBirth = Age.HasValue ? DateTime.Today.AddYears(-Age.Value) : (DateTime?)null,
                        Source = AcquisitionSource,
                        Notes = "Auto-created from salon booking"
                    };

                    var createResult = await _memberService.CreateMemberAsync(_facilityContext.CurrentFacilityId, newClient);
                    if (createResult.IsSuccess)
                    {
                        clientId = createResult.Value;
                        WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<Member>(_facilityContext.CurrentFacilityId));
                    }
                    else
                    {
                        _notificationService.ShowNotification("Could not register new client. Proceeding as guest.", NotificationType.Warning);
                    }
                }
                else if (IsExistingClientMode && SelectedClient != null)
                {
                    clientName = SelectedClient.FullName;
                }

                var appt = new Appointment
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantService.GetTenantId() ?? Guid.Empty,
                    FacilityId = _facilityContext.CurrentFacilityId,
                    ClientId = clientId,
                    ClientName = !string.IsNullOrWhiteSpace(clientName) ? clientName : "Guest",
                    StaffId = SelectedStaffId,
                    StaffName = SelectedStaff.FullName,
                    ServiceId = SelectedService.Id,
                    ServiceName = SelectedService.Name,
                    StartTime = startTime,
                    EndTime = endTime,
                    Price = Price,
                    Status = AppointmentStatus.Scheduled,
                    Notes = Notes ?? string.Empty
                };

                await _salonService.BookAppointmentAsync(appt);
                
                await _mediator.Publish(new Management.Application.Notifications.FacilityActionCompletedNotification(
                    appt.FacilityId, 
                    "Appointment", 
                    appt.ClientName, 
                    _terminologyService.GetTerm("Terminology.Salon.Booking.Success") ?? "Appointment Booked Successfully", 
                    appt.Id.ToString()));

                _modalService.CloseModal();
            }
            catch (Exception ex)
            {
                _notificationService.ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            }
        }
    }
}
