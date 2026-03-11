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
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.ViewModels.Shell
{
    public partial class DashboardViewModel : FacilityAwareViewModelBase, IAsyncViewModel, 
        IRecipient<RefreshRequiredMessage<Sale>>, 
        IRecipient<RefreshRequiredMessage<Member>>,
        IRecipient<RefreshRequiredMessage<PayrollEntry>>,
        IRecipient<RefreshRequiredMessage<InventoryPurchaseDto>>,
        IRecipient<TableStatusChangedMessage>
    {
        private readonly IDashboardService _dashboardService;
        private readonly ISyncService _syncService;
        private readonly IServiceScopeFactory _scopeFactory;
        private CancellationTokenSource? _initCts;
        
        [ObservableProperty]
        private ObservableRangeCollection<PopularItemDto> _popularItems = new();

        private bool _isInitializing;
        private bool _needsRefreshDuringInit;
        private bool _isDirty = true; // Initial load is always dirty

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
        private string _welcomeMessage = "Welcome to Titan Management";

        [ObservableProperty]
        private string _emptyStateMessage = "It looks like you don't have any members yet. Let's get started!";

        [ObservableProperty]
        private string _addButtonText = "Add New Member";

        [ObservableProperty]
        private ISeries[] _occupancySeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _xAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _yAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _occupancyXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _occupancyYAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private bool _isBusinessMode;

        [ObservableProperty]
        private bool _isSalonMode;

        [ObservableProperty]
        private bool _isRestaurantMode;

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
        private ISeries[] _revenueSeries = Array.Empty<ISeries>();

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
            IServiceScopeFactory scopeFactory) 
            : base(terminologyService, facilityContextService, logger, diagnosticService, toastService, localizationService)
        {
            _dashboardService = dashboardService;
            _modalNavigationService = modalNavigationService;
            _serviceProvider = serviceProvider;
            _reportingService = reportingService;
            _syncService = syncService;
            _scopeFactory = scopeFactory;

            _syncService.SyncCompleted += OnSyncCompleted;
            _facilityContext.FacilityChanged += OnFacilityChanged;
            
            IsBusinessMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Gym;
            IsSalonMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Salon;
            IsRestaurantMode = _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Restaurant;

            InitializeStrings();
            
            // Initialize axes with default elements to prevent LiveCharts crashes
            XAxes = new Axis[] { new Axis { TextSize = 12 } };
            YAxes = new Axis[] { new Axis { TextSize = 12 } };

            RegisterMessages();
        }

        private void RegisterMessages()
        {
            // Unregister first to avoid double-registration
            WeakReferenceMessenger.Default.UnregisterAll(this);

            // Register for refresh messages
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<Sale>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<Member>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<PayrollEntry>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<InventoryPurchaseDto>>(this);

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
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownMenuItem") ?? "Revenue by Menu Item";
            }
            else
            {
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
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdown") ?? "Revenue by Plan";
            }
            else if (mode == "Product")
            {
                RevenueBreakdownTitle = _terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownProduct") ?? "Revenue by Product";
            }
            else if (mode == "MenuItem")
            {
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

        private async Task RefreshRevenueBreakdown()
        {
            GetDateRange(SelectedPlanFilter, out var start, out var end);

            List<PlanRevenueDto> data;
            if (IsRestaurantMode && RevenueBreakdownTitle.Contains(_terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownMenuItem") ?? "Menu"))
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                data = await _dashboardService.GetRevenueByMenuItemAsync(facilityId, start, end);
            }
            else if (RevenueBreakdownTitle.Contains(_terminologyService.GetTerm("Terminology.Dashboard.Chart.RevenueBreakdownProduct") ?? "Product"))
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                data = await _dashboardService.GetRevenueByProductAsync(facilityId, start, end);
            }
            else
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                data = await _dashboardService.GetRevenueByPlanAsync(facilityId, start, end);
            }

            PlanRevenue.Clear();
            foreach (var item in data) PlanRevenue.Add(item);
        }

        private async Task RefreshStaffPerformance()
        {
            GetDateRange(SelectedStaffFilter, out var start, out var end);
            var facilityId = _facilityContext.CurrentFacilityId;
            var data = await _dashboardService.GetStaffPerformanceAsync(facilityId, start, end);

            StaffPerformance.Clear();
            foreach (var item in data) StaffPerformance.Add(item);
        }

        [RelayCommand]
        private void ToggleDashboardMode()
        {
            IsBusinessMode = !IsBusinessMode;
        }

        [RelayCommand]
        private void RunPayroll()
        {
            _ = _modalNavigationService.OpenModalAsync<Management.Presentation.ViewModels.Finance.PayrollViewModel>();
        }

        [RelayCommand]
        private void OpenPayrollHistory()
        {
            _ = _modalNavigationService.OpenModalAsync<Management.Presentation.ViewModels.Finance.PayrollHistoryViewModel>();
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
                    "Titan", "Reports");
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
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        ActivePeopleCount = summary.ActiveMembers;
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
                        RecentActivity.ReplaceRange(summary.Activities.Select(activity => 
                        {
                            DateTime timestamp;
                            if (!DateTime.TryParse(activity.Timestamp, out timestamp))
                            {
                                // Fallback to current time if parsing fails (shouldn't happen with ISO8601 but safe to have)
                                timestamp = DateTime.Now;
                            }

                            return new AccessEventDto 
                            { 
                                MemberName = activity.Title, 
                                AccessStatus = activity.Subtitle, 
                                Timestamp = timestamp, 
                                IsAccessGranted = activity.Type == "Member",
                                FailureReason = activity.Subtitle.StartsWith("Denied") ? activity.Subtitle : null
                            };
                        }));

                        RecentTransactions.ReplaceRange(summary.RecentTransactions);
                        StaffPerformance.ReplaceRange(summary.TopPerformingStaff);

                        // Map Revenue Trend
                        if (summary.RevenueTrend?.Any() == true)
                        {
                             RevenueSeries = new ISeries[]
                            {
                                new LineSeries<double>
                                {
                                    Values = new ObservableCollection<double>(summary.RevenueTrend.Select(p => p.Value ?? 0)),
                                    Name = "Revenue",
                                    Fill = new LinearGradientPaint(new SKColor[] { SKColor.Parse("#3B82F6").WithAlpha(50), SKColors.Transparent }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                                    Stroke = new SolidColorPaint(SKColor.Parse("#3B82F6")) { StrokeThickness = 3 },
                                    GeometrySize = 8,
                                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                                    GeometryFill = new SolidColorPaint(SKColor.Parse("#3B82F6")),
                                    LineSmoothness = 1
                                }
                            };
                        }

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
                        _ = RefreshRevenueBreakdown();
                        _ = RefreshStaffPerformance();
                    });
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
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                _isDirty = true;
                _logger?.LogInformation("[Dashboard] Marked dirty due to Sale change.");
                // Reload immediately so dashboard reflects new transactions
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    if (!IsDisposed) await LoadDeferredAsync();
                });
            }
        }

        public void Receive(RefreshRequiredMessage<Member> message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                _isDirty = true;
                _logger?.LogInformation("[Dashboard] Marked dirty due to Member change.");
                // Reload immediately so dashboard member counts update
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    if (!IsDisposed) await LoadDeferredAsync();
                });
            }
        }

        public void Receive(RefreshRequiredMessage<PayrollEntry> message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                _isDirty = true;
                _logger?.LogInformation("[Dashboard] Marked dirty due to Payroll change.");
                // Reload immediately so dashboard expenses update
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    if (!IsDisposed) await LoadDeferredAsync();
                });
            }
        }

        public void Receive(RefreshRequiredMessage<InventoryPurchaseDto> message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                _isDirty = true;
                _logger?.LogInformation("[Dashboard] Marked dirty due to Inventory Purchase change.");
                // Reload immediately so dashboard expenses update
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    if (!IsDisposed) await LoadDeferredAsync();
                });
            }
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
            _ = LoadDeferredAsync();
        }

        private void OnFacilityChanged(Management.Domain.Enums.FacilityType type)
        {
             // Run on UI thread to ensure properties update correctly
             System.Windows.Application.Current.Dispatcher.Invoke(() => 
             {
                 _logger?.LogInformation("[Dashboard] FacilityChanged to {Type}. Resetting state.", type);
                 
                 // Update mode flags based on new context
                 IsBusinessMode = type == Management.Domain.Enums.FacilityType.Gym;
                 IsSalonMode = type == Management.Domain.Enums.FacilityType.Salon;
                 IsRestaurantMode = type == Management.Domain.Enums.FacilityType.Restaurant;
                 
                 // Re-register messages for the new mode (especially important for Restaurant TableStatusChanged)
                 RegisterMessages();
                 
                 ResetState();
             });
        }
    }
}
