using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces.ViewModels;
using Management.Application.Services;
using Management.Domain.Interfaces;
using MediatR;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Domain.Models;
using Management.Domain.Models.Salon;
using Management.Application.DTOs;
using Management.Application.Notifications;
using Management.Presentation.Helpers;
using Management.Presentation.Services.Localization;
using Management.Presentation.Services.Salon;
using Management.Presentation.Services.State;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.ViewModels.Shared;
using Management.Presentation.ViewModels.GymHome;
using Management.Presentation.ViewModels.Members;
using Management.Presentation.ViewModels.Shop;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;

namespace Management.Presentation.ViewModels.Salon
{
    public partial class SalonHomeViewModel : ViewModelBase,
        IFacilityHomeViewModel,
        IStateResettable,
        IRecipient<FacilityActionCompletedMessage>,
        IRecipient<RefreshRequiredMessage<Sale>>,
        IRecipient<RefreshRequiredMessage<Member>>,
        IRecipient<RefreshRequiredMessage<Registration>>,
        IRecipient<RefreshRequiredMessage<PayrollEntry>>,
        IRecipient<RefreshRequiredMessage<InventoryPurchaseDto>>
    {
        private readonly SessionManager _sessionManager;
        private readonly ILocalizationService _localizationService;
        private readonly ISalonService _salonService;
        private readonly ISaleService _saleService;
        private readonly IProductService _productService;
        private readonly IAppointmentService _appointmentService;
        private readonly ISyncService _syncService;
        private readonly IFacilityContextService _facilityContext;
        private readonly Management.Domain.Services.IDialogService _dialogService;
        private readonly ITerminologyService _terminologyService;
        private DispatcherTimer? _clockTimer;
        private CancellationTokenSource? _refreshCts;

        [ObservableProperty]
        private ObservableCollection<Appointment> _todayAgenda = new();

        [ObservableProperty]
        private ObservableRangeCollection<IActivityItem> _activityStream = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoUpcoming))]
        [NotifyPropertyChangedFor(nameof(UpcomingPosition))]
        [NotifyPropertyChangedFor(nameof(HasMultipleUpcoming))]
        [NotifyPropertyChangedFor(nameof(CurrentUpcoming))]
        private Appointment? _nextAppointment;

        public Appointment? CurrentUpcoming => NextAppointment;

        public bool HasNoUpcoming => NextAppointment == null;

        public bool HasMultipleUpcoming => TodayAgenda != null && TodayAgenda.Count > 1;

        public string UpcomingPosition 
        {
            get
            {
                if (NextAppointment == null || TodayAgenda == null || !TodayAgenda.Any()) return "0 / 0";
                var index = TodayAgenda.IndexOf(NextAppointment);
                return $"{index + 1} / {TodayAgenda.Count}";
            }
        }

        [ObservableProperty]
        private string _appointmentsTodayCount = "0";

        public void Receive(FacilityActionCompletedMessage message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<Sale> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<Member> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<Registration> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<PayrollEntry> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        public void Receive(RefreshRequiredMessage<InventoryPurchaseDto> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            HandleRefreshAsync();
        }

        private void HandleRefreshAsync()
        {
            if (IsDisposed) return;

            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var token = _refreshCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested) return;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (IsDisposed || token.IsCancellationRequested) return;
                        await RefreshDataAsync();
                    });
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (IsDisposed || !ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[SalonHome] Sync debounce passed, refreshing dashboard data...");
                await RefreshDataAsync();
            });
        }

        [ObservableProperty]
        private string _totalRevenueToday = "0 DA";

        [ObservableProperty]
        private double _chairUtilization = 0;

        [ObservableProperty]
        private bool _isPrinterOnline = false; // Default to false until checked

        [ObservableProperty]
        private bool _isScannerOnline = false; // Default to false until checked

        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

        [ObservableProperty]
        private string _greeting = string.Empty;



        private string _scanInput = string.Empty;
        public string ScanInput 
        { 
            get => _scanInput; 
            set => SetProperty(ref _scanInput, value); 
        }

        public IAsyncRelayCommand ScanCommand => ProcessScanCommand;

        // ... props ...

        public SalonHomeViewModel(
            ILogger<SalonHomeViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            SessionManager sessionManager,
            ILocalizationService localizationService,
            ISalonService salonService,
            ISaleService saleService,
            IProductService productService,
            IAppointmentService appointmentService,
            IFacilityContextService facilityContext,
            Management.Domain.Services.IDialogService dialogService,
            ITerminologyService terminologyService,
            ISyncService syncService) : base(logger, diagnosticService, toastService)
        {
            _sessionManager = sessionManager;
            _localizationService = localizationService;
            _salonService = salonService;
            _saleService = saleService;
            _productService = productService;
            _appointmentService = appointmentService;
            _facilityContext = facilityContext;
            _dialogService = dialogService;
            _terminologyService = terminologyService;
            _syncService = syncService;

            _syncService.SyncCompleted += OnSyncCompleted;
            _facilityContext.FacilityChanged += OnFacilityChanged;

            _localizationService.LanguageChanged += (s, e) => 
            {
                CurrentDate = DateTime.Now.ToString(_terminologyService.GetTerm("Terminology.Salon.Home.DateFullFormat"), _localizationService.CurrentCulture);
            };

            // Register for Messenger updates
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.RegisterAll(this);

            _salonService.AppointmentStatusChanged += OnAppointmentStatusChanged;

            StartClock();
        }

        private void OnAppointmentStatusChanged(object? sender, (Guid AppointmentId, AppointmentStatus NewStatus) e)
        {
            // Refresh dashboard data when an appointment status changes (e.g. completed -> revenue update)
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => 
            {
                if (!IsLoading) await RefreshDataAsync();
            });
        }

        public async Task InitializeAsync()
        {
            IsActive = true;
            // Set initial clock values on UI Thread
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                CurrentTime = DateTime.Now.ToString("HH:mm:ss");
                CurrentDate = DateTime.Now.ToString(_terminologyService.GetTerm("Terminology.Salon.Home.DateFullFormat"), _localizationService.CurrentCulture);
                UpdateGreeting();
                StartClock();
            });

            await RefreshDataAsync();
        }

        // â”€â”€ Carousel state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private DispatcherTimer? _carouselTimer;

        [RelayCommand]
        private void NextUpcoming()
        {
            if (TodayAgenda == null || !TodayAgenda.Any()) return;
            
            var currentIndex = NextAppointment != null ? TodayAgenda.IndexOf(NextAppointment) : 0;
            if (currentIndex < 0) currentIndex = 0;

            var nextIndex = (currentIndex + 1) % TodayAgenda.Count;
            NextAppointment = TodayAgenda[nextIndex];
            ResetCarouselTimer();
        }

        [RelayCommand]
        private void PreviousUpcoming()
        {
            if (TodayAgenda == null || !TodayAgenda.Any()) return;

            var currentIndex = NextAppointment != null ? TodayAgenda.IndexOf(NextAppointment) : 0;
            if (currentIndex < 0) currentIndex = 0;

            var prevIndex = (currentIndex - 1 + TodayAgenda.Count) % TodayAgenda.Count;
            NextAppointment = TodayAgenda[prevIndex];
            ResetCarouselTimer();
        }

        private void UpdateCarousel(List<Appointment> orderedAgenda)
        {
             if (orderedAgenda.Any())
             {
                 if (NextAppointment == null || !orderedAgenda.Any(a => a.Id == NextAppointment.Id))
                 {
                     NextAppointment = orderedAgenda.FirstOrDefault(a => a.StartTime >= DateTime.Now) ?? orderedAgenda.Last();
                 }
                 StartCarouselTimer();
             }
             else
             {
                 NextAppointment = null;
                 StopCarouselTimer();
             }
        }

        private void StartCarouselTimer()
        {
            if (_carouselTimer == null)
            {
                _carouselTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                _carouselTimer.Tick += (s, e) => NextUpcoming();
            }
            if (!_carouselTimer.IsEnabled)
                _carouselTimer.Start();
        }

        private void StopCarouselTimer()
        {
            _carouselTimer?.Stop();
        }

        private void ResetCarouselTimer()
        {
            StopCarouselTimer();
            StartCarouselTimer();
        }

        private async Task RefreshDataAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                var today = DateTime.Today;
                var utcStart = today.ToUniversalTime();
                var utcEnd = today.AddDays(1).AddTicks(-1).ToUniversalTime();

                var facilityId = _facilityContext.CurrentFacilityId;
                if (facilityId == Guid.Empty)
                {
                    _logger?.LogWarning("[SalonHome] RefreshDataAsync aborted: FacilityId is Guid.Empty.");
                    return;
                }

                _logger.LogInformation($"[SalonHome] Refreshing data for Facility: {facilityId} on {today.ToShortDateString()}");

                // 1. Load Appointments (Uses local dates, but internal logic handles UTC if needed)
                await _salonService.LoadAppointmentsAsync(facilityId, today);
                var appointments = _salonService.Appointments.ToList();
                _logger.LogInformation($"[SalonHome] Appointments Loaded: {appointments.Count}");

                // 1b. Load Completed Appointments for Activity Stream (Today only)
                var pastAppointments = await _appointmentService.GetByRangeAsync(facilityId, today, today.AddDays(1));

                // 2. Load Sales (Today only — UTC boundaries for DB alignment)
                var salesResult = await _saleService.GetSalesByRangeAsync(facilityId, utcStart, utcEnd);
                var sales = salesResult.IsSuccess ? salesResult.Value : new List<SaleDto>();

                // 3. Load Revenue Directly (Bypasses truncation for accurate totals)
                var revenueResult = await _saleService.GetTotalRevenueAsync(facilityId, utcStart, utcEnd);
                var totalRevenue = revenueResult.IsSuccess ? revenueResult.Value : 0m;

                _logger.LogInformation($"[SalonHome] Sales List Loaded: {sales.Count}, Total Revenue: {totalRevenue}");

                // 3. Mapping (Using Shortcuts)
                var stream = new List<IActivityItem>();

                string appointmentWalkInLabel = _terminologyService.GetTerm("Terminology.Salon.Home.Activity.WalkInClient");
                string serviceLabel = _terminologyService.GetTerm("Terminology.Salon.Home.Activity.Service");

                // Add Appointments - ONLY COMPLETED ONES
                int validApps = 0;
                foreach (var appt in pastAppointments.Where(a => a != null && a.Status == AppointmentStatus.Completed))
                {
                    stream.Add(new AppointmentActivityItem(appt, appointmentWalkInLabel, serviceLabel));
                    validApps++;
                }
                _logger.LogInformation($"[SalonHome] Added {validApps} completed appointments to stream.");

                // Add Sales
                foreach (var sale in sales)
                {
                    string primaryItem = _terminologyService.GetTerm("Terminology.Salon.Product.Unknown");
                    int count = 0;
                    
                    // Use Snapshot if available (Backend v2)
                    if (sale.ItemsSnapshot != null && sale.ItemsSnapshot.Any())
                    {
                        primaryItem = sale.ItemsSnapshot.Keys.First();
                        count = sale.ItemsSnapshot.Values.Sum();
                    }
                    else if (sale.Items != null && sale.Items.Any()) // Fallback to ID lookup if needed (but we skipped loading products)
                    {
                        primaryItem = _terminologyService.GetTerm("Terminology.Salon.Product.Unknown") + " (Legacy)";
                        count = sale.Items.Values.Sum();
                    }
                    
                    string saleWalkInLabel = _terminologyService.GetTerm("Terminology.Salon.Home.Activity.WalkInClient");
                    string moreLabel = _terminologyService.GetTerm("Terminology.Salon.Home.Activity.More");
                    string saleLabel = _terminologyService.GetTerm("Terminology.Salon.Home.Activity.Sale");
                    
                    stream.Add(new SaleActivityItem(sale, primaryItem, count, saleWalkInLabel, moreLabel, saleLabel));
                }

                // Sort by Time Ascending (Chronological)
                var orderedStream = stream.OrderByDescending(x => x.SortDate).ToList(); // Use Descending for "Latest First"? 
                // Wait, "Today's Activity" usually shows latest at top? Or chronological?
                // Chat history said: "sorted by time (newest first)". So Descending.
                // Current code was: stream.OrderBy(x => x.SortDate).
                // I will change to OrderByDescending.
                
                ActivityStream = new ObservableRangeCollection<IActivityItem>(orderedStream);

                // Update Agenda for backward compatibility if needed, or just clear it / keep it for Carousel calculation
                // Carousel needs UPCOMING. So OrderBy StartTime is correct for Agenda.
                // WE EXCLUDE COMPLETED AND CANCELLED FROM THE CAROUSEL AGENDA
                var orderedAgenda = appointments
                    .Where(a => a != null && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Completed)
                    .OrderBy(a => a.StartTime)
                    .ToList();
                TodayAgenda = new ObservableCollection<Appointment>(orderedAgenda);
                _logger.LogInformation($"[SalonHome] Agenda updated with {orderedAgenda.Count} upcoming appointments.");

                // Build Carousel
                UpdateCarousel(orderedAgenda);

                // Stats - SHOW COMPLETED APPOINTMENTS COUNT
                var completedToday = appointments.Count(a => a.Status == AppointmentStatus.Completed);
                AppointmentsTodayCount = completedToday.ToString();
                
                // Active minutes...
                var activeMinutes = orderedAgenda
                    .Where(a => a.Status == AppointmentStatus.InProgress || a.Status == AppointmentStatus.Confirmed)
                    .Sum(a => (a.EndTime - a.StartTime).TotalMinutes);
                ChairUtilization = Math.Min(1.0, activeMinutes / 720.0);
                
                // Revenue - Use the authoritative total from the SQL sum
                TotalRevenueToday = $"{totalRevenue:N2} {_terminologyService.GetTerm("Terminology.Global.Currency")}";
            });
        }

        [RelayCommand]
        public async Task ProcessWalkInAsync()
        {
            await _dialogService.ShowCustomDialogAsync<WalkInConfirmationViewModel>();
        }

        [RelayCommand]
        public async Task SellItemAsync()
        {
            await _dialogService.ShowCustomDialogAsync<QuickSaleViewModel>();
        }

        [RelayCommand]
        public async Task RegisterMemberAsync()
        {
            await _dialogService.ShowCustomDialogAsync<QuickRegistrationViewModel>();
        }

        [RelayCommand]
        public async Task OpenMultiSaleCartAsync()
        {
            await _dialogService.ShowCustomDialogAsync<MultiSaleCartViewModel>();
        }

        private void UpdateGreeting()
        {
            var now = DateTime.Now;
            string greetingKey;

            if (now.Hour >= 5 && now.Hour < 12) greetingKey = "Terminology.Salon.Greeting.Morning";
            else if (now.Hour >= 12 && now.Hour < 18) greetingKey = "Terminology.Salon.Greeting.Afternoon";
            else greetingKey = "Terminology.Salon.Greeting.Evening";

            var timeGreeting = _terminologyService.GetTerm(greetingKey);
            var firstName = _sessionManager.CurrentUser?.FullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() 
                            ?? _terminologyService.GetTerm("Terminology.Salon.Greeting.There");

            Greeting = $"{timeGreeting}, {firstName}";
        }

        private void StartClock()
        {
            if (_clockTimer != null) return;
            
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("HH:mm:ss");
                CurrentDate = DateTime.Now.ToString(_terminologyService.GetTerm("Terminology.Salon.Home.DateFullFormat"), _localizationService.CurrentCulture);
                
                // Update greeting every minute or when the hour changes is more efficient, but here we do it every tick for simplicity or when time-aware
                if (DateTime.Now.Second == 0) UpdateGreeting(); 

                // Real-time Status Check Simulation
                // In a real app, this would poll a hardware service
                if (DateTime.Now.Second % 10 == 0) CheckDeviceStatus();
            };
            _clockTimer.Start();
        }

        private void CheckDeviceStatus()
        {
            // Simulate status check logic
            // Ideally: _hardwareService.CheckStatus()
            // For now: Assume online if no errors reported
            IsPrinterOnline = true; // Placeholder for actual check
            IsScannerOnline = true; // Placeholder for actual check
        }

        [RelayCommand]
        public async Task ProcessScanAsync()
        {
            if (string.IsNullOrWhiteSpace(ScanInput)) return;
            
            // Mock salon scan (quick check-in logic)
            var appt = TodayAgenda.FirstOrDefault(a => a.ClientName.Contains(ScanInput, StringComparison.OrdinalIgnoreCase));
            if (appt != null)
            {
                var originalStatus = appt.Status;
                await _salonService.UpdateAppointmentStatusAsync(appt.Id, AppointmentStatus.InProgress);
                
                _toastService.ShowSuccess(
                    string.Format(_terminologyService.GetTerm("Terminology.Salon.Home.CheckIn.Success"), appt.ClientName),
                    async () => 
                    {
                        await _salonService.UpdateAppointmentStatusAsync(appt.Id, originalStatus);
                    },
                    "Undo"
                );
            }
            
            ScanInput = string.Empty;
            await RefreshDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unregister Messenger
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(this);

                if (_facilityContext != null)
                {
                    _facilityContext.FacilityChanged -= OnFacilityChanged;
                }
                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }

                if (_clockTimer != null)
                {
                    _clockTimer.Stop();
                    _clockTimer = null;
                }
                if (_carouselTimer != null)
                {
                    _carouselTimer.Stop();
                    _carouselTimer = null;
                }

                _refreshCts?.Cancel();
                _refreshCts?.Dispose();
                _refreshCts = null;
            }
            base.Dispose(disposing);
        }

        public void ResetState()
        {
             IsActive = false;
             _logger?.LogInformation("Resetting state for SalonHomeViewModel");
             System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
             {
                 TodayAgenda.Clear();
                 ScanInput = string.Empty;
                 AppointmentsTodayCount = "0";
                 TotalRevenueToday = "0 DA";
                 ChairUtilization = 0;
             });
         }

         private void OnFacilityChanged(FacilityType type)
         {
             if (IsDisposed) return;
             _logger?.LogInformation("[SalonHome] FacilityChanged event received ({Type}).", type);
             var newFacilityId = _facilityContext.CurrentFacilityId;
             
             if (newFacilityId != Guid.Empty)
             {
                 _logger?.LogInformation("[SalonHome] FacilityId resolved ({Id}). Reloading data.", newFacilityId);
                 System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => 
                 {
                     if (IsDisposed) return;
                     await RefreshDataAsync();
                 });
             }
         }
    }
}
