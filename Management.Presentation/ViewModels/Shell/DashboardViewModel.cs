using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using Management.Application.Interfaces;
using Management.Application.DTOs;
using Management.Application.Interfaces.ViewModels;
using Management.Application.Interfaces.App;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Microsoft.Extensions.Logging;
using Management.Presentation.Services;
using Management.Domain.Services;
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;
using Management.Domain.Models;
using Management.Presentation.ViewModels.History;
using Microsoft.Extensions.DependencyInjection;
using Management.Presentation.ViewModels.Dashboard;
using Management.Presentation.ViewModels.Finance;

namespace Management.Presentation.ViewModels.Shell
{
    public partial class DashboardViewModel : FacilityAwareViewModelBase, IAsyncViewModel, 
        IRecipient<RefreshRequiredMessage<Sale>>, 
        IRecipient<RefreshRequiredMessage<Member>>,
        IRecipient<RefreshRequiredMessage<Registration>>,
        IRecipient<RefreshRequiredMessage<PayrollEntry>>,
        IRecipient<RefreshRequiredMessage<InventoryPurchaseDto>>,
        IRecipient<FacilityActionCompletedMessage>,
        IRecipient<TableStatusChangedMessage>
    {
        private readonly IDashboardService _dashboardService;
        private readonly ISyncService _syncService;
        private readonly IServiceScopeFactory _scopeFactory;
        private System.Threading.CancellationTokenSource? _refreshDebounceCts;
        private readonly IDispatcher _dispatcher;
        private CancellationTokenSource? _initCts;
        
        [ObservableProperty]
        private ObservableRangeCollection<PopularItemDto> _popularItems = new();

        private bool _isInitializing;
        private bool _needsRefreshDuringInit;
        private bool _isDirty = true; // Initial load is always dirty
        private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private int _activePeopleCount;

        [ObservableProperty]
        private int _totalActiveMembers;

        [ObservableProperty]
        private int _expiringSoonCount;

        [ObservableProperty]
        private int _pendingRegistrationsCount;

        public int PeopleInside => ActivePeopleCount;

        [ObservableProperty]
        private bool _isPrinterOnline = true;

        [ObservableProperty]
        private bool _isScannerOnline = true;

        [ObservableProperty]
        private bool _isSyncActive = true;

        [ObservableProperty]
        private ObservableRangeCollection<AccessEventDto> _recentActivity = new();

        [ObservableProperty]
        private int _totalMembers;

        [ObservableProperty]
        private string _welcomeMessage = "Welcome to Luxurya Management";

        [ObservableProperty]
        private string _emptyStateMessage = "It looks like you don't have any members yet. Let's get started!";

        [ObservableProperty]
        private string _addButtonText = "Add New Member";

        [ObservableProperty]
        private ISeries[] _occupancySeries = Array.Empty<ISeries>();

        // ── Modern Revenue Trend Chart ──────────────────────────────────────
        [ObservableProperty]
        private ISeries[] _revenueSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _revenueXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _revenueYAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private DateTime _selectedRevenueTrendMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        [ObservableProperty]
        private string _formattedRevenueTrendMonth = DateTime.Now.ToString("MMMM yyyy").ToUpper();

        [ObservableProperty]
        private bool _isRevenueTrendLoading;
        // ────────────────────────────────────────────────────────────────────

        [ObservableProperty]
        private Axis[] _xAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _yAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _occupancyXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _occupancyYAxes = Array.Empty<Axis>();

        // ── Retention Guardian (Churn Risk) ──────────────────────────────────
        [ObservableProperty]
        private bool _isRetentionDetailVisible;

        [ObservableProperty]
        private ISeries[] _retentionSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _retentionXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _retentionYAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private string _retentionSummaryText = "Check your community health";
        // ────────────────────────────────────────────────────────────────────

        [ObservableProperty]
        private DateTime _selectedOccupancyDate = DateTime.Today;

        [ObservableProperty]
        private string _formattedOccupancyDate = "TODAY";

        [ObservableProperty]
        private bool _isOccupancyLoading;

        [ObservableProperty]
        private DateTime _selectedMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        [ObservableProperty]
        private string _formattedSelectedMonth = "THIS MONTH";

        [ObservableProperty]
        private ISeries[] _memberSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _memberXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _memberYAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private bool _isBusinessMode;

        partial void OnIsBusinessModeChanged(bool value)
        {
            if (value)
            {
                _ = RefreshRevenueTrendAsync();
                _ = RefreshRevenueBreakdown();
            }
        }

        [ObservableProperty]
        private bool _isSalonMode;

        private int _currentRevenueMode = 0; // 0: Plan, 1: Product, 2: Menu Item

        [ObservableProperty]
        private bool _isRestaurantMode;

        public bool IsGymMode => !IsSalonMode && !IsRestaurantMode;

        [ObservableProperty]
        private int _activeTablesCount;

        [ObservableProperty]
        private int _totalTablesCount;

        [ObservableProperty]
        private int _pendingOrdersCount;

        [ObservableProperty]
        private int _todayCovers;

        [ObservableProperty]
        private decimal _averageOrderValue;

        [ObservableProperty]
        private ObservableRangeCollection<PlanRevenueDto> _planRevenue = new();

        [ObservableProperty]
        private string _selectedPlanFilter = "Today";

        [ObservableProperty]
        private string _revenueBreakdownTitle = "Revenue by Plan";

        [ObservableProperty]
        private FinancialSummaryDto _financialSummary = new();

        [ObservableProperty]
        private double _revenueProgress;


        [ObservableProperty]
        private ObservableRangeCollection<TransactionDto> _recentTransactions = new();

        [ObservableProperty]
        private ObservableRangeCollection<ChurnRiskDto> _churnRisks = new();

        [ObservableProperty]
        private int _activeClientsThisMonth;

        [ObservableProperty]
        private ObservableRangeCollection<StaffPerformanceDto> _staffPerformance = new();

        [ObservableProperty]
        private int _todayAppointmentsTotal;

        [ObservableProperty]
        private int _todayAppointmentsCompleted;

        [ObservableProperty]
        private int _todayAppointmentsPending;

        [ObservableProperty]
        private string _selectedStaffFilter = "Today";

        private readonly IModalNavigationService _modalNavigationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IReportingService _reportingService;
        private readonly IEmailService _emailService;
        private readonly ISecureStorageService _secureStorage;

        public DashboardViewModel(
            IDashboardService dashboardService, 
            ITerminologyService terminologyService,
            ILogger<DashboardViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IFacilityContextService facilityContextService,
            IModalNavigationService modalNavigationService,
            IServiceProvider serviceProvider,
            IReportingService reportingService,
            ILocalizationService localizationService,
            ISyncService syncService,
            IServiceScopeFactory scopeFactory,
            IDispatcher dispatcher,
            IEmailService emailService,
            ISecureStorageService secureStorage) 
            : base(terminologyService, facilityContextService, logger, diagnosticService, toastService, localizationService)
        {
            _refreshDebounceCts = new System.Threading.CancellationTokenSource();
            _dashboardService = dashboardService;
            _modalNavigationService = modalNavigationService;
            UpdateFormattedMonth();
            _serviceProvider = serviceProvider;
            _reportingService = reportingService;
            _syncService = syncService;
            _scopeFactory = scopeFactory;
            _dispatcher = dispatcher;
            _emailService = emailService;
            _secureStorage = secureStorage;
            
            // Register for Messenger updates
            WeakReferenceMessenger.Default.RegisterAll(this);

            _syncService.SyncCompleted += OnSyncCompleted;
            _facilityContext.FacilityChanged += OnFacilityChanged;
            
            IsBusinessMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Gym;
            IsSalonMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Salon;
            IsRestaurantMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Restaurant;

            InitializeStrings();
            
            // Initialize all chart axes to empty arrays to prevent startup deadlocks during DI
            XAxes = Array.Empty<Axis>();
            YAxes = Array.Empty<Axis>();
            
            OccupancyXAxes = Array.Empty<Axis>();
            OccupancyYAxes = Array.Empty<Axis>();

            RegisterMessages();
        }

        private void RegisterMessages()
        {
            // Unregister first to avoid double-registration
            WeakReferenceMessenger.Default.UnregisterAll(this);

            // Register for refresh messages
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<Sale>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<Member>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<Registration>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<PayrollEntry>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<InventoryPurchaseDto>>(this);
            WeakReferenceMessenger.Default.Register<FacilityActionCompletedMessage>(this);

            // Restaurant: refresh revenue & table counts instantly when any table changes state
            if (IsRestaurantMode)
            {
                _logger?.LogInformation("[Dashboard] Registering for TableStatusChangedMessage (Restaurant Mode)");
                WeakReferenceMessenger.Default.Register<TableStatusChangedMessage>(this);
            }
        }

        private void InitializeStrings()
        {
            Title = _terminologyService.GetTerm("Terminology.Dashboard.Title");
            WelcomeMessage = _terminologyService.GetTerm("Terminology.Dashboard.Welcome");
            EmptyStateMessage = _terminologyService.GetTerm("Terminology.Dashboard.EmptyState");
            AddButtonText = _terminologyService.GetTerm("Terminology.Dashboard.AddMember");
            
            if (IsRestaurantMode)
            {
                _currentRevenueMode = 2; // Menu Item
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownMenuItem") ?? "Revenue by Menu Item";
            }
            else
            {
                _currentRevenueMode = 0; // Plan
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdown") ?? "Revenue by Plan";
            }
        }

        protected override void OnLanguageChanged()
        {
            InitializeStrings();
        }

        [RelayCommand]
        private void SetRevenueMode(string mode)
        {
            if (IsRestaurantMode && mode != "MenuItem") return;

            if (mode == "Plan")
            {
                _currentRevenueMode = 0;
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdown") ?? "Revenue by Plan";
            }
            else if (mode == "Product")
            {
                _currentRevenueMode = 1;
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownProduct") ?? "Revenue by Product";
            }
            else if (mode == "MenuItem")
            {
                _currentRevenueMode = 2;
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownMenuItem") ?? "Revenue by Menu Item";
            }
            
            _ = RefreshRevenueBreakdown();
        }

        [RelayCommand]
        private async Task SetPlanFilter(string filter)
        {
            SelectedPlanFilter = filter;
            await RefreshRevenueBreakdown();
        }

        [RelayCommand]
        private async Task SetStaffFilter(string filter)
        {
            SelectedStaffFilter = filter;
            await RefreshStaffPerformance();
        }

        private void GetDateRange(string filter, out DateTime start, out DateTime end)
        {
            var today = DateTime.Today;
            switch (filter)
            {
                case "This Week":
                    start = today.AddDays(-(int)today.DayOfWeek);
                    end = start.AddDays(7);
                    break;
                case "This Month":
                    start = new DateTime(today.Year, today.Month, 1);
                    end = start.AddMonths(1);
                    break;
                case "Today":
                default:
                    start = today;
                    end = today.AddDays(1);
                    break;
            }
        }

        [RelayCommand]
        private void ToggleRetentionView()
        {
            IsRetentionDetailVisible = !IsRetentionDetailVisible;
        }
       

        private async Task RefreshRetentionDataAsync(DashboardSummaryDto summary)
        {
            if (summary?.ChurnRisks == null) return;

            await _dispatcher.InvokeAsync(() =>
            {
                ChurnRisks.ReplaceRange(summary.ChurnRisks);

                // Update Summary Text
                var highRiskCount = summary.ChurnRisks.Count(r => r.RiskLevel == "High");
                if (highRiskCount > 0)
                    RetentionSummaryText = $"{highRiskCount} members are at high risk of churning.";
                else
                    RetentionSummaryText = summary.ChurnRisks.Any() 
                        ? $"{summary.ChurnRisks.Count} members need re-engagement."
                        : "Your community health looks great!";

                // Build simple distribution chart for the "Graph" tab
                var highCount = summary.ChurnRisks.Count(r => r.RiskLevel == "High");
                var medCount = summary.ChurnRisks.Count(r => r.RiskLevel == "Medium");

                if (highCount == 0 && medCount == 0)
                {
                    // "All Clear" Solid Teal Chart
                    RetentionSeries = new ISeries[]
                    {
                        new PieSeries<int>
                        {
                            Name = "Healthy",
                            Values = new[] { 100 },
                            Fill = new SolidColorPaint(SKColor.Parse("#10B981")), // Emerald/Teal
                            InnerRadius = 60,
                            DataLabelsPaint = null
                        }
                    };
                }
                else
                {
                    RetentionSeries = new ISeries[]
                    {
                        new PieSeries<int>
                        {
                            Name = "High Risk",
                            Values = new[] { highCount },
                            Fill = new SolidColorPaint(SKColor.Parse("#EF4444")), // Red
                            InnerRadius = 60
                        },
                        new PieSeries<int>
                        {
                            Name = "Medium Risk",
                            Values = new[] { medCount },
                            Fill = new SolidColorPaint(SKColor.Parse("#F59E0B")), // Amber
                            InnerRadius = 60
                        }
                    };
                }
            });
        }
        private async Task RefreshRevenueTrendAsync()
        {
            if (CurrentFacilityId == Guid.Empty) return;

            try
            {
                IsRevenueTrendLoading = true;
                _logger?.LogInformation("[Dashboard] Refreshing High-Fidelity Revenue Trend for Facility: {FacilityId}", CurrentFacilityId);

                // 1. Precise Month Calculation
                var monthStart = new DateTime(SelectedRevenueTrendMonth.Year, SelectedRevenueTrendMonth.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                int daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

                // 2. Surgical Data Fetching (Facility Aware)
                List<DateTimePoint> trend;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                    // Check mode to decide which table to aggregate from
                    if (IsRestaurantMode)
                    {
                        // Custom logic for restaurant orders (Menu + Tax)
                        trend = await svc.GetRevenueTrendAsync(CurrentFacilityId, monthStart, monthEnd); 
                        // Note: Internal DashboardService already branched for Restaurant if implemented correctly, 
                        // but we ensure context is passed.
                    }
                    else
                    {
                        // Default logic for Gym/Salon (Sales Table)
                        trend = await svc.GetRevenueTrendAsync(CurrentFacilityId, monthStart, monthEnd);
                    }
                }

                if (trend == null) trend = new List<DateTimePoint>();

                // 3. Dense Data Mapping (Zero-Gap)
                var values = new double[daysInMonth];
                var labels = new string[daysInMonth];
                for (int i = 0; i < daysInMonth; i++)
                {
                    int day = i + 1;
                    labels[i] = day.ToString();
                    
                    // Match by Day with safety for month-end overlaps
                    var point = trend.FirstOrDefault(p => p.DateTime.Date == monthStart.AddDays(i).Date);
                    values[i] = point?.Value ?? 0;
                }

                // 4. Premium Visual Styling
                await _dispatcher.InvokeAsync(() =>
                {
                    RevenueSeries = new ISeries[]
                    {
                        new LineSeries<double>
                        {
                            Name = "Revenue",
                            Values = new System.Collections.ObjectModel.ObservableCollection<double>(values),
                            Stroke = new SolidColorPaint(SKColor.Parse("#8B5CF6")) { StrokeThickness = 4 },
                            Fill = new LinearGradientPaint(
                                new[] { SKColor.Parse("#8B5CF6").WithAlpha(60), SKColors.Transparent },
                                new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                            GeometrySize = 10,
                            GeometryStroke = new SolidColorPaint(SKColor.Parse("#8B5CF6")) { StrokeThickness = 2 },
                            GeometryFill = new SolidColorPaint(SKColors.White),
                            LineSmoothness = 0.5,
                            YToolTipLabelFormatter = point => $"{point.Model:N2} DA"
                        }
                    };

                    RevenueXAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = labels,
                            LabelsPaint = new SolidColorPaint(SKColor.Parse("#A1A1AA")),
                            TextSize = 11,
                            MinStep = 1,
                            SeparatorsPaint = null,
                            Padding = new LiveChartsCore.Drawing.Padding(0, 10, 0, 0)
                        }
                    };

                    RevenueYAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => $"{value:N0} DA",
                            LabelsPaint = new SolidColorPaint(SKColor.Parse("#A1A1AA")),
                            SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#F1F5F9")) { StrokeThickness = 1 },
                            TextSize = 11,
                            MinLimit = 0
                        }
                    };

                    FormattedRevenueTrendMonth = SelectedRevenueTrendMonth.ToString("MMMM yyyy").ToUpper();
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to refresh High-Fidelity Revenue Trend");
            }
            finally
            {
                IsRevenueTrendLoading = false;
            }
        }

        [RelayCommand]
        private async Task PreviousRevenueTrendMonth()
        {
            SelectedRevenueTrendMonth = SelectedRevenueTrendMonth.AddMonths(-1);
            await RefreshRevenueTrendAsync();
        }

        [RelayCommand]
        private async Task NextRevenueTrendMonth()
        {
            var now = DateTime.Now;
            if (SelectedRevenueTrendMonth.Year >= now.Year && SelectedRevenueTrendMonth.Month >= now.Month)
                return;

            SelectedRevenueTrendMonth = SelectedRevenueTrendMonth.AddMonths(1);
            await RefreshRevenueTrendAsync();
        }

        private async Task RefreshRevenueBreakdown()
        {
            GetDateRange(SelectedPlanFilter, out var start, out var end);

            var facilityId = _facilityContext.CurrentFacilityId;

            List<PlanRevenueDto> data;
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedDashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                
                if (IsRestaurantMode && _currentRevenueMode == 2)
                {
                    data = await scopedDashboardService.GetRevenueByMenuItemAsync(facilityId, start, end);
                }
                else if (_currentRevenueMode == 1)
                {
                    data = await scopedDashboardService.GetRevenueByProductAsync(facilityId, start, end);
                }
                else
                {
                    data = await scopedDashboardService.GetRevenueByPlanAsync(facilityId, start, end);
                }
            }

            PlanRevenue.Clear();
            foreach (var item in data) PlanRevenue.Add(item);
        }

        private async Task RefreshStaffPerformance()
        {
            GetDateRange(SelectedStaffFilter, out var start, out var end);
            var facilityId = _facilityContext.CurrentFacilityId;
            
            List<StaffPerformanceDto> data;
            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedDashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                data = await scopedDashboardService.GetStaffPerformanceAsync(facilityId, start, end);
            }

            StaffPerformance.Clear();
            foreach (var item in data) StaffPerformance.Add(item);
        }

        [RelayCommand]
        private async Task CycleRevenueMode()
        {
            // Cycle: Plan (0) -> Product (1) -> [MenuItem (2) if Restaurant] -> Plan (0)
            if (_currentRevenueMode == 0) // From Plan to Product
            {
                _currentRevenueMode = 1;
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownProduct") ?? "Revenue by Product";
            }
            else if (_currentRevenueMode == 1 && IsRestaurantMode) // From Product to Menu
            {
                _currentRevenueMode = 2;
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownMenuItem") ?? "Revenue by Menu Item";
            }
            else // Back to Plan
            {
                _currentRevenueMode = 0;
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdown") ?? "Revenue by Plan";
            }

            await RefreshRevenueBreakdown();
        }



        [RelayCommand]
        private async Task ToggleDashboardMode()
        {
            IsBusinessMode = !IsBusinessMode;
            if (IsBusinessMode)
            {
                await RefreshRevenueTrendAsync();
                await RefreshRevenueBreakdown();
            }
        }

        [RelayCommand]
        private void RunPayroll()
        {
            _ = _modalNavigationService.OpenModalAsync<Management.Presentation.ViewModels.Finance.PayrollViewModel>();
        }

        [RelayCommand]
        private void OpenPayrollHistory()
        {
            _ = _modalNavigationService.OpenModalAsync<PayrollHistoryViewModel>();
        }

        [RelayCommand]
        private void OpenRevenueHistory()
        {
            _ = _modalNavigationService.OpenModalAsync<RevenueHistoryViewModel>();
        }

        [RelayCommand]
        private void OpenInventoryHistory()
        {
            // Only for Gym and Salon
            if (_facilityContext.CurrentFacility is Management.Domain.Enums.FacilityType.Gym or Management.Domain.Enums.FacilityType.Salon)
            {
                _ = _modalNavigationService.OpenModalAsync<InventoryHistoryViewModel>();
            }
            else
            {
                _toastService.ShowInfo("Inventory history is available for Retail environments.");
            }
        }

        [RelayCommand]
        private void OpenOccupancyHistory()
        {
            _ = _modalNavigationService.OpenModalAsync<OccupancyHistoryViewModel>();
        }

        [RelayCommand]
        private async Task ExportReport()
        {
            try
            {
                _toastService.ShowInfo("Generating PDF report…", "Export");

                var facilityId = _facilityContext.CurrentFacilityId;
                var snapshot   = await _reportingService.GetDailySnapshotAsync(facilityId, DateTime.Today);
                var pdfBytes   = await _reportingService.GenerateDailyPdfReportAsync(snapshot);

                // Save to Documents\Titan\Reports\
                var reportsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Luxurya", "Reports");
                Directory.CreateDirectory(reportsFolder);

                var fileName = $"DailyReport_{DateTime.Today:yyyy_MM_dd}_{DateTime.Now:HHmmss}.pdf";
                var filePath = Path.Combine(reportsFolder, fileName);

                await File.WriteAllBytesAsync(filePath, pdfBytes);

                // Open the PDF with the default viewer
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = filePath,
                    UseShellExecute = true
                });

                _toastService.ShowSuccess($"PDF saved: {fileName}", "Export Successful");
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Failed to export PDF report: " + ex.Message, "Export Failed");
            }
        }


        public Task PreInitializeAsync()
        {
            InitializeStrings();
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask; // Phased out in favor of lifecycle

        public async Task LoadDeferredAsync()
        {
            IsActive = true;

            // FIX Step 3.1: Guard against loading before FacilityId is resolved
            if (_facilityContext.CurrentFacilityId == Guid.Empty)
            {
                _logger?.LogWarning("[Dashboard] LoadDeferredAsync aborted: FacilityId is Guid.Empty (Not yet resolved).");
                return;
            }

            if (!_isDirty && !_isInitializing) return;

            if (_isInitializing)
            {
                _needsRefreshDuringInit = true;
                _logger?.LogInformation("[Dashboard] Refresh requested while initializing. Queuing catch-up refresh.");
                return;
            }

            await ExecuteLoadingAsync(async () =>
            {
                do
                {
                    _isInitializing = true;
                    _needsRefreshDuringInit = false;
                    _isDirty = false;

                    _initCts?.Cancel();
                    _initCts = new CancellationTokenSource();
                    
                    try
                    {
                        _logger?.LogInformation("[Dashboard] Loading data... (Facility: {Id})", _facilityContext.CurrentFacilityId);
                    
                    DashboardSummaryDto summary;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var scopedDashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                        summary = await scopedDashboardService.GetSummaryAsync();
                    }
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => 
                    {
                        ActivePeopleCount = summary.CheckInsToday;
                        TotalActiveMembers = summary.ActiveMembers; 
                        TotalMembers = summary.TotalMembers;
                        ExpiringSoonCount = summary.ExpiringSoonCount;
                        PendingRegistrationsCount = summary.PendingRegistrationsCount;

                        ActiveTablesCount = summary.ActiveTablesCount;
                        TotalTablesCount = summary.TotalTablesCount;
                        PendingOrdersCount = summary.PendingOrdersCount;
                        TodayCovers = summary.TodayCovers;
                        AverageOrderValue = summary.AverageOrderValue;

                        TodayAppointmentsTotal = summary.TodayAppointmentsTotal;
                        TodayAppointmentsCompleted = summary.TodayAppointmentsCompleted;
                        TodayAppointmentsPending = summary.TodayAppointmentsPending;
                        ActiveClientsThisMonth = summary.ActiveClientsThisMonth;

                        PopularItems.ReplaceRange(summary.PopularItems);
                        // Non-destructive update: Add missing items instead of wiping everything
                        var existingIds = new HashSet<DateTime>(RecentActivity.Select(a => a.Timestamp));
                        var newItems = summary.Activities.Select(activity => 
                        {
                            DateTime timestamp;
                            if (!DateTime.TryParse(activity.Timestamp, out timestamp)) timestamp = DateTime.Now;

                            return new AccessEventDto 
                            { 
                                MemberName = activity.Title, 
                                AccessStatus = activity.Subtitle, 
                                Timestamp = timestamp, 
                                IsAccessGranted = activity.Type == "Member",
                                FailureReason = activity.Subtitle.StartsWith("Denied") ? activity.Subtitle : null
                            };
                        }).Where(a => !existingIds.Contains(a.Timestamp)).ToList();

                        if (newItems.Any())
                        {
                            foreach (var item in newItems.OrderBy(a => a.Timestamp))
                            {
                                RecentActivity.Insert(0, item);
                            }
                            while (RecentActivity.Count > 50) RecentActivity.RemoveAt(RecentActivity.Count - 1);
                        }

                        RecentTransactions.ReplaceRange(summary.RecentTransactions);
                        StaffPerformance.ReplaceRange(summary.TopPerformingStaff);

                        
                        
                        // Milestone 1: Refresh Retention Data from Summary
                        _ = RefreshRetentionDataAsync(summary);

                        // Revenue Trend is now managed by RefreshRevenueTrendAsync() — see below.

                        // Map Occupancy/Member Trend
                        if (summary.MemberTrend?.Any() == true)
                        {
                             OccupancySeries = new ISeries[]
                            {
                                new LineSeries<double>
                                {
                                    Values = new ObservableCollection<double>(summary.MemberTrend.Select(p => p.Value ?? 0)),
                                    Name = IsRestaurantMode ? "Hourly Table Occupancy" : (IsSalonMode ? "Active Appointments" : "Real-time Capacity"),
                                    Fill = new LinearGradientPaint(new SKColor[] { SKColor.Parse("#10B981").WithAlpha(50), SKColors.Transparent }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                                    Stroke = new SolidColorPaint(SKColor.Parse("#10B981")) { StrokeThickness = 3 },
                                    GeometrySize = 8,
                                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                                    GeometryFill = new SolidColorPaint(SKColor.Parse("#10B981")),
                                    LineSmoothness = 1
                                }
                            };

                            OccupancyXAxes = new Axis[] 
                            { 
                                new Axis 
                                { 
                                    Labels = summary.MemberTrend.Select(p => p.DateTime.ToString("HH:mm")).ToArray(),
                                    LabelsRotation = 0,
                                    SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
                                } 
                            };
                            
                            OccupancyYAxes = new Axis[] 
                            { 
                                new Axis 
                                { 
                                    Labeler = value => value.ToString("N0"),
                                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E5E7EB")) { StrokeThickness = 1 }
                                } 
                            };
                        }
                        
                        LoadFinancialData(summary);
                        
                        // Revenue Trend must run OUTSIDE the dispatcher block
                        // to avoid a nested-dispatcher deadlock (see RefreshRevenueTrendAsync).
                        if (IsBusinessMode || IsRestaurantMode) await RefreshRevenueBreakdown();
                        if (IsBusinessMode || IsSalonMode) await RefreshStaffPerformance();
                        
                        await RefreshMemberDevelopmentAsync();
                        await RefreshOccupancyTrendAsync();
                    });

                    // Revenue Trend: runs on the calling thread, then marshals UI updates
                    // through its own _dispatcher.InvokeAsync — safe outside the outer dispatcher block.
                    if (IsBusinessMode || IsRestaurantMode) await RefreshRevenueTrendAsync();
                }
                finally
                {
                    _isInitializing = false;
                }
                
                // If a refresh was requested while we were busy, run it again until settled
                } while (_needsRefreshDuringInit);

            }, "Failed to update dashboard data.");
        }

        private void LoadFinancialData(DashboardSummaryDto summary)
        {
            FinancialSummary = new FinancialSummaryDto
            {
                NetProfit = summary.NetProfit,
                NetProfitPercentChange = summary.NetProfitPercentChange,
                Revenue = summary.DailyRevenue, // Mapped to daily for the snap shot card
                RevenuePercentChange = summary.RevenuePercentChange,
                Expenses = summary.DailyExpenses,
                ExpensesPercentChange = summary.ExpensesPercentChange,
                MembershipsRevenue = IsRestaurantMode ? 0 : summary.MonthlyRevenue * 0.8m,
                MerchandiseRevenue = IsRestaurantMode ? 0 : summary.MonthlyRevenue * 0.2m,
                Salaries = 0,
                Rent = 0,
                Utilities = 0
            };

            if (summary.DailyRevenueTarget > 0)
            {
                RevenueProgress = (double)(summary.DailyRevenue / summary.DailyRevenueTarget);
                if (RevenueProgress > 1) RevenueProgress = 1;
            }
            else
            {
                RevenueProgress = 0;
            }
        }

        public void Receive(RefreshRequiredMessage<Sale> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;

            // Coalesce rapid-fire messages into a single refresh
            _refreshDebounceCts?.Cancel();
            _refreshDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _refreshDebounceCts.Token;

            _isDirty = true;
            _logger?.LogInformation("[Dashboard] Marked dirty, debouncing refresh...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested || IsDisposed) return;
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsDisposed) await LoadDeferredAsync();
                    });
                }
                catch (TaskCanceledException) { }
            }, token);
        }

       public void Receive(RefreshRequiredMessage<Member> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            
            _refreshDebounceCts?.Cancel();
            _refreshDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _refreshDebounceCts.Token;

            _isDirty = true;
            _logger?.LogInformation("[Dashboard] Marked dirty (Member), debouncing refresh...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested || IsDisposed) return;
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsDisposed) await LoadDeferredAsync();
                    });
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        public void Receive(RefreshRequiredMessage<Registration> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;

            _isDirty = true;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (!IsDisposed) await LoadDeferredAsync();
            });
        }

        public async void Receive(FacilityActionCompletedMessage message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;

            // 1. Optimistic update for the activity list
            var activity = new AccessEventDto
            {
                MemberName = message.DisplayName,
                AccessStatus = message.Message,
                Timestamp = DateTime.Now,
                IsAccessGranted = message.ActionType == "Access" || message.ActionType == "Registration" || message.ActionType == "Walk-In",
                FailureReason = (message.ActionType == "Access" && message.Message.Contains("Denied") ? message.Message : null) ?? string.Empty
            };

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (RecentActivity.Count >= 50) RecentActivity.RemoveAt(RecentActivity.Count - 1);
                RecentActivity.Insert(0, activity);
            });

            // 2. Immediate Stat Refresh (Off-UI Thread, Throttled)
            await ExecuteBackgroundAsync(async () =>
            {
                DashboardSummaryDto summary;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedDashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                    summary = await scopedDashboardService.GetSummaryAsync();
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    ActivePeopleCount = summary.CheckInsToday;
                    TotalActiveMembers = summary.ActiveMembers;
                    TotalMembers = summary.TotalMembers;
                    ExpiringSoonCount = summary.ExpiringSoonCount;
                    PendingRegistrationsCount = summary.PendingRegistrationsCount;
                });
            }, _refreshSemaphore);
        }

        public void Receive(RefreshRequiredMessage<PayrollEntry> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            
            _refreshDebounceCts?.Cancel();
            _refreshDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _refreshDebounceCts.Token;

            _isDirty = true;
            _logger?.LogInformation("[Dashboard] Marked dirty (Payroll), debouncing refresh...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token);
                    if (token.IsCancellationRequested || IsDisposed) return;
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsDisposed) await LoadDeferredAsync();
                    });
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        public void Receive(RefreshRequiredMessage<InventoryPurchaseDto> message)
        {
            if (message.Value != _facilityContext.CurrentFacilityId) return;
            
            _refreshDebounceCts?.Cancel();
            _refreshDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _refreshDebounceCts.Token;

            _isDirty = true;
            _logger?.LogInformation("[Dashboard] Marked dirty (Inventory), debouncing refresh...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested || IsDisposed) return;
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsDisposed) await LoadDeferredAsync();
                    });
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        /// <summary>
        /// Restaurant-only: Refresh the dashboard when any table changes state
        /// (order started, order paid, table reset). Only registered when IsRestaurantMode is true.
        /// </summary>
        public void Receive(TableStatusChangedMessage message)
        {
            if (!IsRestaurantMode) return;
            if (message.Value != _facilityContext.CurrentFacilityId) return;

            _isDirty = true;
            _logger?.LogInformation("[Dashboard] Marked dirty due to restaurant TableStatusChanged.");
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                if (!IsDisposed) await LoadDeferredAsync();
            });
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (!ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[Dashboard] Sync debounce passed, refreshing data...");
                _isDirty = true;
                await LoadDeferredAsync();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshDebounceCts?.Cancel();
                _refreshDebounceCts?.Dispose();
                WeakReferenceMessenger.Default.UnregisterAll(this);
                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }
                if (_facilityContext != null)
                {
                    _facilityContext.FacilityChanged -= OnFacilityChanged;
                }
            }
            base.Dispose(disposing);
        }

        public override void ResetState()
        {
            base.ResetState();
            IsActive = false;
            
            // Ensure facility mode flags are updated upon facility switch
            IsBusinessMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Gym;
            IsSalonMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Salon;
            IsRestaurantMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Restaurant;

            InitializeStrings();
            _isDirty = true;
            // Removed redundant `_ = LoadDeferredAsync()` call.
            // When FacilityChanged fires, it updates properties and calls ResetState.
            // If the view is active, the data will be loaded naturally via the NavigationService lifecycle
            // or by explicit user navigation, preventing a double-load race condition.
        }

        private void OnFacilityChanged(Management.Domain.Enums.FacilityType type)
        {
             // Run on UI thread to ensure properties update correctly
             _dispatcher.InvokeAsync(async () => 
             {
                 var newFacilityId = _facilityContext.CurrentFacilityId;
                 _logger?.LogInformation("[Dashboard] FacilityChanged to {Type} (ID: {Id}). Resetting state.", type, newFacilityId);
                 
                 // Update mode flags based on new context
                 IsBusinessMode = type == Management.Domain.Enums.FacilityType.Gym;
                 IsSalonMode = type == Management.Domain.Enums.FacilityType.Salon;
                 IsRestaurantMode = type == Management.Domain.Enums.FacilityType.Restaurant;
                 
                 // Re-register messages for the new mode (especially important for Restaurant TableStatusChanged)
                 RegisterMessages();
                 
                 ResetState();

                 // FIX Step 3.2: If we now have a valid ID, trigger data loading
                 if (newFacilityId != Guid.Empty)
                 {
                     _logger?.LogInformation("[Dashboard] FacilityId resolved to {Id}. Triggering data load.", newFacilityId);
                     
                     // RESET Month to current upon facility change
                     SelectedMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                     UpdateFormattedMonth();
                     
                     // Optimization: If we're already on UI thread and this is initial load, 
                     // consider if we should fire-and-forget to avoid blocking the transition.
                     await LoadDeferredAsync();
                 }
             });
        }

        [RelayCommand]
        private async Task PreviousMonthAsync()
        {
            SelectedMonth = SelectedMonth.AddMonths(-1);
            await RefreshMemberDevelopmentAsync();
            if (IsBusinessMode) await RefreshRevenueTrendAsync();
        }

        [RelayCommand]
        private async Task NextMonthAsync()
        {
            var now = DateTime.Now;
            if (SelectedMonth.Month == now.Month && SelectedMonth.Year == now.Year)
                return;

            SelectedMonth = SelectedMonth.AddMonths(1);
            await RefreshMemberDevelopmentAsync();
            if (IsBusinessMode) await RefreshRevenueTrendAsync();
        }

        partial void OnSelectedMonthChanged(DateTime value) => UpdateFormattedMonth();

        private void UpdateFormattedMonth()
        {
            var now = DateTime.Now;
            if (SelectedMonth.Month == now.Month && SelectedMonth.Year == now.Year)
                FormattedSelectedMonth = "THIS MONTH";
            else
                FormattedSelectedMonth = SelectedMonth.ToString("MMMM yyyy").ToUpper();
        }

        private async Task RefreshMemberDevelopmentAsync()
        {
             if (CurrentFacilityId == Guid.Empty) return;

             try 
             {
                 var counts = await _dashboardService.GetWeeklyMemberGrowthAsync(CurrentFacilityId, SelectedMonth.Year, SelectedMonth.Month);
                 
                 // High-performance track height logic
                 double maxVal = counts.Any() ? counts.Max() : 0;
                 double trackVal = maxVal > 10 ? maxVal * 1.15 : 10; 

                 // 1. Unified Track Series (Background)
                 var trackSeries = new ColumnSeries<double>
                 {
                     Values = new[] { trackVal, trackVal, trackVal, trackVal },
                     Fill = new SolidColorPaint(SKColor.Parse("#F1F5F9")), // Light Gray Track
                     Rx = 80, Ry = 80,
                     MaxBarWidth = 40,
                     IgnoresBarPosition = true,
                     ZIndex = -1,
                     IsHoverable = false,
                     DataLabelsPaint = null
                 };


                 // 2. Multi-color Weekly Fill Series
                 // Colors: Cyan, Indigo, Emerald, Violet
                 var colors = new[] { "#06B6D4", "#6366F1", "#10B981", "#8B5CF6" };
                 var seriesList = new List<ISeries> { trackSeries };

                 for (int i = 0; i < 4; i++)
                 {
                     double[] values = new double[4];
                     values[i] = counts[i];

                     seriesList.Add(new ColumnSeries<double>
                     {
                         Name = $"Week {i + 1}",
                         Values = values,
                         Fill = new SolidColorPaint(SKColor.Parse(colors[i])),
                         Rx = 80, Ry = 80,
                         MaxBarWidth = 40,
                         IgnoresBarPosition = true,
                         Padding = 0,
                         ZIndex = 100 // Data always on top
                     });
                 }

                 MemberSeries = seriesList.ToArray();

                 MemberXAxes = new[] {
                     new Axis { 
                         Labels = new[] { "week 1", "week 2", "week 3", "week 4" },
                         LabelsPaint = new SolidColorPaint(SKColor.Parse("#71717A")),
                         TextSize = 12,
                         Padding = new LiveChartsCore.Drawing.Padding(0, 15, 0, 0)
                     }
                 };
                 MemberYAxes = new[] {
                     new Axis { 
                         LabelsPaint = new SolidColorPaint(SKColor.Parse("#71717A")),
                         MinLimit = 0, // Strictly enforce zero start
                         MaxLimit = trackVal > 0 ? trackVal : 10,
                         MinStep = 1.0, 
                         IsVisible = true,
                         TextSize = 12,
                         Padding = new LiveChartsCore.Drawing.Padding(10, 0, 10, 0)
                     }
                 };
             }
             catch (Exception ex)
             {
                 _logger?.LogError(ex, "[Dashboard] Failed to refresh Weekly Member Development chart");
             }
        }

        private void UpdateFormattedOccupancyDate()
        {
            if (SelectedOccupancyDate.Date == DateTime.Today)
                FormattedOccupancyDate = "TODAY";
            else if (SelectedOccupancyDate.Date == DateTime.Today.AddDays(-1))
                FormattedOccupancyDate = "YESTERDAY";
            else
                FormattedOccupancyDate = SelectedOccupancyDate.ToString("ddd, MMM dd").ToUpper();
        }

        // Called automatically by CommunityToolkit.Mvvm whenever SelectedOccupancyDate changes.
        // This covers the DatePicker binding path, which bypasses the command methods.
        partial void OnSelectedOccupancyDateChanged(DateTime value)
        {
            UpdateFormattedOccupancyDate();
            _ = RefreshOccupancyTrendAsync();
        }

        [RelayCommand]
        private void SetOccupancyFilter(string filter)
        {
            // Setting SelectedOccupancyDate triggers OnSelectedOccupancyDateChanged automatically.
            if (filter == "Today") SelectedOccupancyDate = DateTime.Today;
            else if (filter == "Yesterday") SelectedOccupancyDate = DateTime.Today.AddDays(-1);
        }

        [RelayCommand]
        private void SelectCustomOccupancyDate(DateTime date)
        {
            // Setting SelectedOccupancyDate triggers OnSelectedOccupancyDateChanged automatically.
            SelectedOccupancyDate = date;
        }

        private async Task RefreshOccupancyTrendAsync()
        {
            if (CurrentFacilityId == Guid.Empty || !IsGymMode) return;

            try
            {
                IsOccupancyLoading = true;
                var trend = await _dashboardService.GetGymOccupancyTrendAsync(CurrentFacilityId, SelectedOccupancyDate);
                
                if (trend != null && (trend.Any() || SelectedOccupancyDate != DateTime.Today))
                {
                    OccupancySeries = new ISeries[]
                    {
                        new LineSeries<double>
                        {
                            Values = new ObservableCollection<double>(trend.Select(p => p.Value ?? 0)),
                            Name = "Live Capacity",
                            Fill = new LinearGradientPaint(
                                new SKColor[] { SKColor.Parse("#10B981").WithAlpha(40), SKColors.Transparent }, 
                                new SKPoint(0.5f, 0), 
                                new SKPoint(0.5f, 1)),
                            Stroke = new SolidColorPaint(SKColor.Parse("#10B981")) { StrokeThickness = 3 },
                            GeometrySize = 0,
                            LineSmoothness = 0.5,
                            EnableNullSplitting = false
                        }
                    };

                    OccupancyXAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = trend.Select(p => p.DateTime.ToString("HH:mm")).ToArray(),
                            LabelsRotation = 0,
                            LabelsPaint = new SolidColorPaint(SKColor.Parse("#71717A")),
                            SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
                        }
                    };

                    OccupancyYAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => value.ToString("N0"),
                            LabelsPaint = new SolidColorPaint(SKColor.Parse("#71717A")),
                            SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E5E7EB")) { StrokeThickness = 1 },
                            MinLimit = 0
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing occupancy trend");
            }
            finally
            {
                IsOccupancyLoading = false;
            }
        }

        [RelayCommand]
        private async Task ReEngageMember(ChurnRiskDto member)
        {
            if (member == null) return;

            // 1. Check if professional email is configured
            var senderEmail = _secureStorage.Get("ProfessionalEmailAccount");
            if (string.IsNullOrEmpty(senderEmail))
            {
                _toastService.ShowWarning(
                    "Professional Email Not Configured", 
                    "To send re-engagement emails, you must register a professional domain account in Settings > Profile.");
                return;
            }

            try
            {
                _toastService.ShowInfo($"Preparing re-engagement for {member.MemberName}...", "Retention Guardian");

                var subject = $"We miss you at {CurrentFacility}!";
                var body = $@"
                    <div style='font-family: sans-serif; color: #1E293B;'>
                        <h2 style='color: #10B981;'>Hello {member.MemberName.Split(' ')[0]}!</h2>
                        <p>We noticed it's been over two weeks since your last visit. We'd love to see you back!</p>
                        <p>As a valued member, we're holding your spot. Come by this week for a session.</p>
                        <br/>
                        <p>Best regards,<br/>The {CurrentFacility} Team</p>
                    </div>";

                await _emailService.SendEmailAsync(member.Email, subject, body);
                
                _toastService.ShowSuccess(
                    $"Re-engagement email sent successfully to {member.MemberName}.", 
                    "Retention Guardian");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send re-engagement email");
                _toastService.ShowError("Failed to send email. Check your API key and domain health in Settings.");
            }
        }
    }

    public partial class MonthFilterViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private DateTime _date;

        [ObservableProperty]
        private bool _isSelected;
    }
}
