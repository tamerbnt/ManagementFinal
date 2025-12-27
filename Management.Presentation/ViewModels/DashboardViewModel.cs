using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // For Point, PointCollection, Clipboard
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading; // For Live Simulation Timer
using Management.Application.Services;// Assumed Service Layer
using Management.Domain.DTOs;            // Assumed DTOs
using Management.Domain.Services; // Assumed Interface Layer
using Management.Presentation.Extensions; // For RelayCommand/ViewModelBase
using Management.Presentation.Services;
using Management.Presentation.ViewModels; // For sub-VMs (ChartPoint)
using Clipboard = System.Windows.Clipboard;
using Point = System.Windows.Point;
using PointCollection = System.Windows.Media.PointCollection;

namespace Management.Presentation.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService; // ADD THIS
        private readonly IMemberService _memberService;
        private readonly IRegistrationService _registrationService;
        private readonly IAccessEventService _accessEventService;
        private readonly DispatcherTimer _liveSimulationTimer;

        // --- 1. STATE PROPERTIES (Metrics) ---

        private int _activePeopleCount;
        public int ActivePeopleCount
        {
            get => _activePeopleCount;
            set => SetProperty(ref _activePeopleCount, value);
        }

        private int _totalActiveMembers;
        public int TotalActiveMembers
        {
            get => _totalActiveMembers;
            set => SetProperty(ref _totalActiveMembers, value);
        }

        private int _expiringSoonCount;
        public int ExpiringSoonCount
        {
            get => _expiringSoonCount;
            set => SetProperty(ref _expiringSoonCount, value);
        }

        private int _pendingRegistrationsCount;
        public int PendingRegistrationsCount
        {
            get => _pendingRegistrationsCount;
            set => SetProperty(ref _pendingRegistrationsCount, value);
        }

        private int _totalMembers;
        public int TotalMembers
        {
            get => _totalMembers;
            set => SetProperty(ref _totalMembers, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _hasData;
        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        // --- 2. COLLECTIONS (Feeds & Charts) ---

        public ObservableCollection<AccessEventDto> ActivityFeed { get; }
            = new ObservableCollection<AccessEventDto>();

        // We wrap DTOs in a ViewModel to attach commands (Approve/Decline) per item
        public ObservableCollection<RegistrationItemViewModel> Registrations { get; }
            = new ObservableCollection<RegistrationItemViewModel>();

        // Chart Data
        private PointCollection _occupancyPoints;
        public PointCollection OccupancyPoints
        {
            get => _occupancyPoints;
            set => SetProperty(ref _occupancyPoints, value);
        }

        public ObservableCollection<ChartPointViewModel> DataPoints { get; }
            = new ObservableCollection<ChartPointViewModel>();

        // --- 3. COMMANDS ---

        public ICommand PrintReportCommand { get; }
        public ICommand AddMemberCommand { get; }
        public ICommand NavigateToRegistrationsCommand { get; }
        public ICommand NavigateToReportsCommand { get; }
        public ICommand QuickCheckInCommand { get; }
        public ICommand CopyChartDataCommand { get; }
        public ICommand SaveChartImageCommand { get; }

        // --- 4. CONSTRUCTOR & INITIALIZATION ---

        public DashboardViewModel(
            INavigationService navigationService,
            IDialogService dialogService, // INJECT THIS
            IMemberService memberService,
            IRegistrationService registrationService,
            IAccessEventService accessEventService)
        {
            _navigationService = navigationService;
            _memberService = memberService;
            _registrationService = registrationService;
            _accessEventService = accessEventService;

            // Initialize Commands
            PrintReportCommand = new RelayCommand(ExecutePrintReport);
            AddMemberCommand = new RelayCommand(ExecuteAddMember);
            NavigateToRegistrationsCommand = new RelayCommand(() => _navigationService.NavigateToAsync(3));
            NavigateToReportsCommand = new RelayCommand(() => _navigationService.NavigateToAsync(5));
            QuickCheckInCommand = new RelayCommand(ExecuteQuickCheckIn);
            CopyChartDataCommand = new RelayCommand(ExecuteCopyChartData);
            SaveChartImageCommand = new RelayCommand(ExecuteSaveChartImage);

            // Initialize Simulation Timer (Runs every 5s to mimic live entry)
            _liveSimulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _liveSimulationTimer.Tick += OnSimulationTick;

            // Load Data
            LoadDashboardDataAsync();
        }

        // --- 5. DATA LOADING & MOCK LOGIC ---

        private async void LoadDashboardDataAsync()
        {
            IsLoading = true;
            HasData = false;

            try
            {
                // Simulate Network Latency (Skeleton UI Showcase)
                await Task.Delay(1500);

                // In a real app, these would be Service calls. 
                // Here we generate realistic Mock Data for the prototype.
                GenerateMockMetrics();
                GenerateMockFeed();
                GenerateMockRegistrations();
                GenerateMockChartData();

                HasData = true;
                _liveSimulationTimer.Start();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void GenerateMockMetrics()
        {
            ActivePeopleCount = 42;
            TotalActiveMembers = 245;
            ExpiringSoonCount = 12;
            PendingRegistrationsCount = 5;
            TotalMembers = 310; // >0 triggers "Content" state instead of "Welcome" state
        }

        private void GenerateMockFeed()
        {
            ActivityFeed.Clear();
            ActivityFeed.Add(new AccessEventDto { MemberName = "John Doe", FacilityName = "Main Gym", AccessStatus = "Granted", IsAccessGranted = true, Timestamp = DateTime.Now.AddMinutes(-2) });
            ActivityFeed.Add(new AccessEventDto { MemberName = "Sarah Connor", FacilityName = "Pool", AccessStatus = "Granted", IsAccessGranted = true, Timestamp = DateTime.Now.AddMinutes(-15) });
            ActivityFeed.Add(new AccessEventDto { MemberName = "Mike Ross", FacilityName = "Main Gym", AccessStatus = "Denied", IsAccessGranted = false, Timestamp = DateTime.Now.AddMinutes(-45) });
        }

        private void GenerateMockRegistrations()
        {
            Registrations.Clear();
            var mocks = new RegistrationItemViewModel[] { /* ... */ };

            foreach (var item in mocks)
            {
                // FIX: Update the command to use DialogService
                item.ViewDetailsCommand = new AsyncRelayCommand(async () =>
                    await _dialogService.ShowCustomDialogAsync<RegistrationDetailViewModel>(item.Id));

                Registrations.Add(item);
            }
        }

        private void GenerateMockChartData()
        {
            // Chart Dimensions (Virtual Canvas)
            double canvasWidth = 1000;
            double canvasHeight = 320;
            double xStep = canvasWidth / 24; // 24 hours

            var rawPoints = new PointCollection();
            DataPoints.Clear();

            // Simulate Bell Curve Occupancy (Peak at 18:00)
            for (int hour = 0; hour <= 24; hour++)
            {
                double occupancy = 0;

                // Simple bell curve math
                if (hour >= 6 && hour <= 22)
                {
                    double x = hour - 14; // Shift peak
                    occupancy = 50 * Math.Exp(-(x * x) / 50); // Gaussian
                }

                // Add some noise
                occupancy = Math.Max(0, occupancy + (new Random().Next(-2, 3)));

                // Map to Canvas Coords (Y is inverted in WPF)
                double plotX = hour * xStep;
                double plotY = canvasHeight - (occupancy * (canvasHeight / 60)); // 60 is max scale

                rawPoints.Add(new Point(plotX, plotY));

                // Add Tooltip Dots for even hours
                if (hour % 6 == 0)
                {
                    DataPoints.Add(new ChartPointViewModel
                    {
                        X = plotX,
                        Y = plotY,
                        TooltipText = $"{hour:00}:00 - {(int)occupancy} people"
                    });
                }
            }

            OccupancyPoints = rawPoints;
        }

        // --- 6. LIVE SIMULATION LOGIC ---

        private void OnSimulationTick(object sender, EventArgs e)
        {
            // Add a new random event to top of list
            var names = new[] { "Bruce Wayne", "Diana Prince", "Clark Kent", "Barry Allen" };
            var rnd = new Random();
            var isGranted = rnd.NextDouble() > 0.1; // 90% success

            var newEvent = new AccessEventDto
            {
                MemberName = names[rnd.Next(names.Length)],
                FacilityName = "Main Gym",
                AccessStatus = isGranted ? "Granted" : "Denied",
                IsAccessGranted = isGranted,
                Timestamp = DateTime.Now
            };

            // Insert at 0 to trigger SlideDown animation in View
            ActivityFeed.Insert(0, newEvent);

            // Keep list size manageable
            if (ActivityFeed.Count > 50) ActivityFeed.RemoveAt(ActivityFeed.Count - 1);

            // Update Counter
            if (isGranted) ActivePeopleCount++;
        }

        // --- 7. ACTION HANDLERS ---

        private void ExecutePrintReport()
        {
            // Call Print Service
            // DialogService.ShowToast("Printer", "Sending report to printer...");
        }

        private void ExecuteAddMember()
        {
            // Open Modal
            // _modalService.Show<AddMemberViewModel>();
        }

        private void ExecuteQuickCheckIn()
        {
            // _modalService.Show<QuickCheckInViewModel>();
        }

        private void ExecuteCopyChartData()
        {
            Clipboard.SetText("Time\tOccupancy\n00:00\t0\n06:00\t12...");
            // DialogService.ShowToast("Success", "Chart data copied to clipboard");
        }

        private void ExecuteSaveChartImage()
        {
            // Implementation to render Visual to Bitmap and save
        }

        private void ApproveRegistration(RegistrationItemViewModel item)
        {
            Registrations.Remove(item);
            PendingRegistrationsCount--;
            // DialogService.ShowToast("Success", $"{item.FullName} approved");
        }

        private void DeclineRegistration(RegistrationItemViewModel item)
        {
            Registrations.Remove(item);
            PendingRegistrationsCount--;
            // DialogService.ShowToast("Info", $"{item.FullName} declined");
        }
    }

    // --- HELPER VIEW MODELS (Nested for cohesive file structure) ---

    public class RegistrationItemViewModel : ViewModelBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; }
        public string Source { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICommand ApproveCommand { get; set; }
        public ICommand DeclineCommand { get; set; }
        public ICommand ViewDetailsCommand { get; set; }
    }

    public class ChartPointViewModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string TooltipText { get; set; }
    }
}