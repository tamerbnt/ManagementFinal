using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // For Point, PointCollection, Clipboard
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading; // For Live Simulation Timer
using Management.Presentation.Services;
using Management.Domain.DTOs;            // Assumed DTOs
using Management.Domain.Enums;
using Management.Domain.Services; // Assumed Interface Layer
using Management.Presentation.Extensions; 
using Management.Presentation.ViewModels; 
using Management.Presentation.Services.Restaurant;
using MediatR;
using Management.Application.Features.Dashboard.Queries.GetDashboardMetrics;
using Clipboard = System.Windows.Clipboard;
using Point = System.Windows.Point;
using PointCollection = System.Windows.Media.PointCollection;

namespace Management.Presentation.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IMediator _mediator;
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;

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

        // Restaurant Metrics
        private int _activeOrdersCount;
        public int ActiveOrdersCount
        {
            get => _activeOrdersCount;
            set => SetProperty(ref _activeOrdersCount, value);
        }

        private decimal _todayRevenue;
        public decimal TodayRevenue
        {
            get => _todayRevenue;
            set => SetProperty(ref _todayRevenue, value);
        }

        private double _occupancyPercentage;
        public double OccupancyPercentage
        {
            get => _occupancyPercentage;
            set => SetProperty(ref _occupancyPercentage, value);
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

        public ObservableCollection<RegistrationItemViewModel> Registrations { get; }
            = new ObservableCollection<RegistrationItemViewModel>();

        // Chart Data
        private PointCollection _occupancyPoints = new();
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
            IDialogService dialogService,
            IMediator mediator)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _mediator = mediator;

            // Initialize Commands
            PrintReportCommand = new RelayCommand(ExecutePrintReport);
            AddMemberCommand = new RelayCommand(ExecuteAddMember);
            NavigateToRegistrationsCommand = new RelayCommand(() => _navigationService.NavigateToAsync(3));
            NavigateToReportsCommand = new RelayCommand(() => _navigationService.NavigateToAsync(5));
            QuickCheckInCommand = new RelayCommand(ExecuteQuickCheckIn);
            CopyChartDataCommand = new RelayCommand(ExecuteCopyChartData);
            SaveChartImageCommand = new RelayCommand(ExecuteSaveChartImage);

            // Load Data
            LoadDashboardDataAsync();
        }

        // --- 5. DATA LOADING engine ---

        private async void LoadDashboardDataAsync()
        {
            IsLoading = true;
            HasData = false;

            try
            {
                // ARCHITECTURAL FIX: MediatR Query replaces 7 redundant service injections.
                // Feature Handler manages multi-tenant context and parallel aggregation internally.
                var metrics = await _mediator.Send(new GetDashboardMetricsQuery());

                TotalActiveMembers = metrics.TotalActiveMembers;
                PendingRegistrationsCount = metrics.PendingRegistrationsCount;
                ActivePeopleCount = metrics.ActivePeopleCount;
                
                // Facility-specific metrics (Resolved in Application Layer)
                ActiveOrdersCount = metrics.ActiveOrdersCount;
                TodayRevenue = metrics.TodayRevenue;
                OccupancyPercentage = metrics.OccupancyPercentage;

                Registrations.Clear();
                foreach (var reg in metrics.RecentRegistrations)
                {
                    var vm = new RegistrationItemViewModel
                    {
                        Id = reg.Id,
                        FullName = reg.FullName,
                        Source = reg.Source,
                        PhoneNumber = reg.PhoneNumber,
                        CreatedAt = reg.CreatedAt
                    };
                    vm.ViewDetailsCommand = new AsyncRelayCommand(async () =>
                        await _dialogService.ShowCustomDialogAsync<RegistrationDetailViewModel>(vm.Id));
                    
                    Registrations.Add(vm);
                }

                ActivityFeed.Clear();
                foreach (var evt in metrics.ActivityFeed) ActivityFeed.Add(evt);

                HasData = true;
            }
            catch (Exception)
            {
                // Handle error
            }
            finally
            {
                IsLoading = false;
            }
        }

        // --- 6. LIVE SIMULATION LOGIC ---
        // REMOVED FOR PRODUCTION
        // Real-time updates should be handled via SignalR or Polling in a real Service
        // For now, checks are manual or periodic via LoadDashboardDataAsync

        // --- 7. ACTION HANDLERS ---

        private void ExecutePrintReport()
        {
            // Call Print Service
            // DialogService.ShowToast("Printer", "Sending report to printer...");
        }

        private async void ExecuteAddMember()
        {
             // Open Modal for New Member (Pass null ID)
             await _dialogService.ShowCustomDialogAsync<MemberDetailViewModel>(null);
        }

        private async void ExecuteQuickCheckIn()
        {
            await _dialogService.ShowCustomDialogAsync<CheckInViewModel>();
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
        public string FullName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public ICommand? ApproveCommand { get; set; }
        public ICommand? DeclineCommand { get; set; }
        public ICommand? ViewDetailsCommand { get; set; }
    }

    public class ChartPointViewModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string TooltipText { get; set; } = string.Empty;
    }
}