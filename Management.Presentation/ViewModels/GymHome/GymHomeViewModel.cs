using System;
using Management.Application.Interfaces.ViewModels;
using System.Collections.ObjectModel;
using Management.Domain.Enums;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces;
using Management.Domain.Services;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Presentation.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Management.Presentation.Helpers;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Management.Presentation.ViewModels.PointOfSale;
using Management.Application.Notifications;
using MediatR;
using Management.Presentation.ViewModels.Members;
using Management.Presentation.ViewModels.Shop;
using Management.Presentation.Services.State;
using Microsoft.Extensions.DependencyInjection; // Added for IServiceScopeFactory
using Management.Domain.Interfaces; // Added for IStateResettable
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Shared;
using Management.Presentation.ViewModels.Base;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;
using Management.Application.Messages;

namespace Management.Presentation.ViewModels.GymHome
{
    public enum SidebarMode
    {
        Guide,
        Alerts
    }

    public partial class GymHomeViewModel : FacilityAwareViewModelBase, IFacilityHomeViewModel, IStateResettable, 
        IRecipient<FacilityActionCompletedMessage>,
        IRecipient<RefreshRequiredMessage<Sale>>,
        IRecipient<RefreshRequiredMessage<Member>>,
        IRecipient<RefreshRequiredMessage<Registration>>,
        IRecipient<RefreshRequiredMessage<PayrollEntry>>,
        IRecipient<RefreshRequiredMessage<InventoryPurchaseDto>>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SessionManager _sessionManager;
        private readonly IAccessEventService _accessEventService;
        private readonly ISyncService _syncService;
        private readonly INotificationService _notificationService;
        private readonly IMessenger _messenger;
        private bool _remoteWelcomeShown = false;
        
        public bool IsRemoteMode => _sessionManager.IsRemoteMode;

        private readonly LiveChartsCore.Defaults.ObservableValue _occupancyValue = new(0);
        private readonly LiveChartsCore.Defaults.ObservableValue _remainingValue = new(100);

        [ObservableProperty]
        private ObservableRangeCollection<IActivityItem> _activityStream = new();

        [ObservableProperty]
        private string _scanInput = string.Empty;
        [ObservableProperty]
        private int _occupancyCount;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private double _occupancyPercentage;

        public IEnumerable<ISeries> OccupancySeries { get; set; }

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

        [ObservableProperty]
        private string _greetingText = string.Empty;

        [ObservableProperty]
        private string _greetingLabel = string.Empty; // e.g. "GOOD MORNING"

        [ObservableProperty]
        private string _greetingName = string.Empty;  // e.g. "Bentouati."

        [ObservableProperty]
        private string _occupancyTrendText = string.Empty;

        [ObservableProperty]
        private bool _isTrendPositive = true;

        [ObservableProperty]
        private bool _hasOccupancyTrend = false;

        [ObservableProperty]
        private int _maxCapacity = 100; // Default fallback

        [ObservableProperty]
        private bool _isOccupancyOverflow;

        [ObservableProperty]
        private int _activeMembersTotal;

        [ObservableProperty]
        private int _expiringSoonCount;

        [ObservableProperty]
        private int _pendingRegistrationsCount;

        [ObservableProperty]
        private decimal _cashBoxTotal;

        [ObservableProperty]
        private bool _isPrinterOnline = true;

        [ObservableProperty]
        private bool _isScannerOnline = true;

        [ObservableProperty]
        private bool _isSyncActive = false;

        [ObservableProperty]
        private decimal _revenueToday;

        [ObservableProperty]
        private decimal _dailyRevenueTarget = 10_000m;

        [ObservableProperty]
        private double _revenueTodayProgress; // 0.0 – 1.0

        // ── Expiring Soon enrichment ──────────────────────────────
        [ObservableProperty]
        private string _expiringSoonRatioText = string.Empty; // e.g. "50 from 200"

        [ObservableProperty]
        private double _expiringSoonPct; // 0-100

        [ObservableProperty]
        private bool _isHighExpiry; // true when > 20%

        // ── Active Members delta ──────────────────────────────────
        [ObservableProperty]
        private string _activeMembersDeltaText = string.Empty; // e.g. "▲ +8 vs yesterday"

        [ObservableProperty]
        private bool _isMembersTrendPositive = true;

        [ObservableProperty]
        private bool _hasMembersTrend;

        [ObservableProperty]
        private KpiMetricDto _ptUpsellRate = new();

        public IEnumerable<ISeries> DemographicSeries { get; set; }

        [ObservableProperty]
        private bool _isScanSuccessful;

        [ObservableProperty]
        private bool _isScanError;

        [ObservableProperty]
        private string _environmentState = "Normal";

        [ObservableProperty]
        private ObservableCollection<double> _occupancySparklineData = new();

        [ObservableProperty]
        private ObservableCollection<double> _revenueSparklineData = new();

        public IEnumerable<ISeries> OccupancyTrendSeries { get; set; }
        public IEnumerable<Axis> XAxes { get; set; }
        public IEnumerable<Axis> YAxes { get; set; }

        [ObservableProperty]
        private ObservableCollection<ActiveMemberAvatarViewModel> _activeAvatars = new();

        [ObservableProperty]
        private int _extraActiveCount;

        [ObservableProperty]
        private bool _hasActiveAvatars;
        
        [ObservableProperty]
        private bool _isActivityEmpty;

        [ObservableProperty]
        private SidebarMode _currentSidebarMode = SidebarMode.Guide;

        [ObservableProperty]
        private ObservableCollection<string> _systemAlerts = new();

        [ObservableProperty]
        private ObservableCollection<GuideTipViewModel> _guideTips = new();

        [ObservableProperty]
        private GuideTipViewModel? _currentGuide;

        [ObservableProperty]
        private int _currentGuideIndex;

        private DispatcherTimer? _carouselTimer;

        private DispatcherTimer? _clockTimer;
        // Debounce token for HandleRefresh — coalesces rapid-fire RefreshRequiredMessages
        // (e.g. Sale + FacilityAction arriving within the same checkout commit)
        // into a single DB round-trip 300ms later.
        private CancellationTokenSource? _refreshDebounceCts;

        private bool _isInitializing;
        private bool _initialized;
        private bool _isDirty;
        private bool _needsRefreshDuringInit;
        private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

        public GymHomeViewModel(
            IServiceScopeFactory scopeFactory, 
            Management.Domain.Services.IDialogService dialogService,
            ILogger<GymHomeViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            SessionManager sessionManager,
            IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            IAccessEventService accessEventService,
            ISyncService syncService,
            INotificationService notificationService,
            IMessenger messenger) : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService, dialogService)
        {
            _scopeFactory = scopeFactory;
            _sessionManager = sessionManager;
            _accessEventService = accessEventService;
            _syncService = syncService;
            _notificationService = notificationService;
            _messenger = messenger;

            _syncService.SyncCompleted += OnSyncCompleted;
            _facilityContext.FacilityChanged += OnFacilityChanged;

            _localizationService.LanguageChanged += (s, e) => 
            {
                CurrentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy", _localizationService.CurrentCulture);
                
                // Re-resolve status strings for existing activity items using their stored key
                foreach (var item in ActivityStream.OfType<Management.Presentation.ViewModels.Shared.ActivityLogItem>())
                {
                    if (!string.IsNullOrEmpty(item.StatusResourceKey))
                    {
                        var resolved = System.Windows.Application.Current.TryFindResource(item.StatusResourceKey) as string;
                        if (resolved != null) item.Status = resolved;
                    }
                }

                // Refresh dynamic content
                InitializeGuideTips();
                _ = PopulateSystemAlertsAsync();
                
                // Refresh KPI texts
                UpdateExpiringSoon();
                UpdateMembersDelta(ActiveMembersTotal); // This might need a better way to get 'yesterday' but for now it's okay for re-localizing
                UpdateOccupancy(OccupancyCount);
            };
            
            // Lightweight initialization ONLY
            ActivityStream = new ObservableRangeCollection<IActivityItem>();
            OccupancySeries = Array.Empty<ISeries>();
            OccupancyTrendSeries = Array.Empty<ISeries>();
            XAxes = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.LightGray), TextSize = 10 } };
            YAxes = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.LightGray), TextSize = 12 } };

            
            // Register for Messenger updates
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.RegisterAll(this);

            // Setup Guide Tips Carousel
            InitializeGuideTips();
            StartCarousel();

            // Initial load - Clock only, stats deferred to Loaded event
            StartClock();

            // Guard the initial visual state for immediate Skeleton display
            IsLoading = true;
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cancel any pending debounced refresh
                _refreshDebounceCts?.Cancel();
                _refreshDebounceCts?.Dispose();
                _refreshDebounceCts = null;

                // Unregister Messenger
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(this);

                if (_carouselTimer != null)
                {
                    _carouselTimer.Stop();
                    _carouselTimer = null;
                }

                if (_clockTimer != null)
                {
                    _clockTimer.Stop();
                    _clockTimer = null;
                }

                if (_facilityContext != null)
                {
                    _facilityContext.FacilityChanged -= OnFacilityChanged;
                }
                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }
            }
            base.Dispose(disposing);
        }

        private void StartClock()
        {
            if (_clockTimer != null) return;

            // Compute greeting once immediately
            RefreshGreeting();

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("HH:mm:ss");
                CurrentDate = DateTime.Now.ToString("D", System.Globalization.CultureInfo.CurrentUICulture);
                RefreshGreeting();
            };
            _clockTimer.Start();
        }

        private void RefreshGreeting()
        {
            var hour = DateTime.Now.Hour;
            string salutation = hour switch
            {
                >= 5 and < 12  => GetResource("Terminology.Home.Greeting.Morning",  "Good Morning"),
                >= 12 and < 18 => GetResource("Terminology.Home.Greeting.Afternoon", "Good Afternoon"),
                _              => GetResource("Terminology.Home.Greeting.Evening",   "Good Evening")
            };

            var name = _sessionManager?.CurrentUser?.FullName?.Split(' ').FirstOrDefault() ?? string.Empty;

            // Legacy combined property (kept for backward compat)
            GreetingText = string.IsNullOrEmpty(name) ? salutation : $"{salutation}, {name}";

            // Split properties for the redesigned 3-layer greeting card
            GreetingLabel = salutation;
            GreetingName  = name;
        }

        private static string GetResource(string key, string fallback)
        {
            return System.Windows.Application.Current.TryFindResource(key) as string ?? fallback;
        }

        private async Task LoadDashboardStatsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                var facilityId = _facilityContext.CurrentFacilityId;

                if (facilityId == Guid.Empty)
                {
                    _logger?.LogWarning("[GymHome] LoadDashboardStatsAsync aborted: FacilityId is Guid.Empty.");
                    return;
                }

                DashboardSummaryDto? summary;
                DailyStatsDto? stats = null;

                if (_sessionManager.IsRemoteMode)
                {
                    _logger?.LogInformation("[GymHome] Remote Mode active. Fetching cloud snapshot for facility {Id}...", facilityId);
                    summary = await dashboardService.GetRemoteSummaryAsync(facilityId);
                    
                    if (summary != null)
                    {
                        // Map cloud snapshot back to stats for UI consistency
                        stats = new DailyStatsDto
                        {
                            OccupancyCount = summary.CheckInsToday,
                            OccupancyLastHour = summary.PeopleInsideLastHour,
                            DailyCashTotal = summary.DailyRevenue,
                            MaxCapacity = 100 // Default fallback for remote view
                        };
                    }
                    else
                    {
                        _logger?.LogWarning("[GymHome] Remote Mode: No cloud snapshot found. Requesting priority push from local data.");
                        _messenger.Send(new SyncRequestedMessage(facilityId));
                    }
                }
                else
                {
                    var operationService = scope.ServiceProvider.GetRequiredService<IGymOperationService>();
                    // Local Mode: Execute sequentially to prevent EF Core DbContext concurrency exceptions
                    stats = await operationService.GetDailyStatsAsync(facilityId);
                    summary = await dashboardService.GetSummaryAsync(facilityId);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (stats != null)
                    {
                        MaxCapacity = stats.MaxCapacity;
                        UpdateOccupancy(stats.OccupancyCount, stats.OccupancyLastHour);
                        RevenueToday = stats.DailyCashTotal;
                    }

                    if (summary != null)
                    {
                        ActiveMembersTotal = summary.ActiveMembers;
                        ExpiringSoonCount = summary.ExpiringSoonCount;
                        PendingRegistrationsCount = summary.PendingRegistrationsCount;
                        DailyRevenueTarget = summary.DailyRevenueTarget > 0 ? summary.DailyRevenueTarget : 10_000m;
                        UpdateRevenueProgress();
                        UpdateExpiringSoon();
                        UpdateMembersDelta(summary.ActiveMembersYesterday);

                        // Show Remote Welcome Notification
                        if (_sessionManager.IsRemoteMode && !_remoteWelcomeShown && summary.LastUpdatedAt != default)
                        {
                            _remoteWelcomeShown = true;
                            var infoFormat = GetResource("Terminology.Dashboard.Stat.RemoteDataInfo", "You are viewing remote data. Last cloud update: {0}");
                            _notificationService.ShowInfo(string.Format(infoFormat, summary.LastUpdatedAt.ToString("MMM dd, HH:mm")));
                        }
                    }

                    // Populate Avatars (Only in Local Mode - we don't sync individual member presence yet)
                    if (!_sessionManager.IsRemoteMode)
                    {
                        _ = UpdateActiveAvatarsAsync(facilityId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load dashboard stats");
            }
        }

        [RelayCommand]
        public async Task OnLoadedAsync()
        {
            await InitializeAsync();
        }

        /// <summary>
        /// Standard interface initialization (foreground/non-silent).
        /// </summary>
        public Task InitializeAsync() => InitializeAsync(silent: false);

        /// <summary>
        /// Main initialization entry point with option for silent background refresh.
        /// </summary>
        public async Task InitializeAsync(bool silent)
        {
            if (_initialized && !_isDirty && !silent) return;
            
            if (_isInitializing) 
            {
                _needsRefreshDuringInit = true;
                return;
            }

            _isInitializing = true;
            try 
            {
                do
                {
                    _needsRefreshDuringInit = false;

                    if (silent)
                    {
                        await ExecuteBackgroundAsync(async () => await PerformInitializationInternalAsync(), _refreshSemaphore);
                    }
                    else
                    {
                        IsActive = true;
                        // ExecuteLoadingAsync already handles IsLoading flag synchronously
                        await ExecuteLoadingAsync(async () =>
                        {
                            // Full foreground loads also wait for any background refresh to finish
                            await _refreshSemaphore.WaitAsync();
                            try 
                            {
                                await PerformInitializationInternalAsync();
                            }
                            finally 
                            { 
                                _refreshSemaphore.Release(); 
                            }
                        });
                    }
                    
                    _initialized = true;
                    _isDirty = false;

                    // If a refresh was queued while we were fetching data, force the next loop to be silent.
                    // We don't want skeleton UI to flicker.
                    if (_needsRefreshDuringInit) silent = true;

                } while (_needsRefreshDuringInit);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async Task PerformInitializationInternalAsync()
        {
            // 1. Prepare Data & Visuals on Background Thread
            var now = DateTime.Now;
            var timeStr = now.ToString("HH:mm:ss");
            var dateStr = now.ToString("dddd, MMMM dd, yyyy");

            // Resolve dynamic accent color for LiveCharts SkiaSharp series
            var accentColor = SKColors.DeepSkyBlue;
            if (System.Windows.Application.Current.TryFindResource("FacilityAccentColor") is System.Windows.Media.Color mediaColor)
            {
                accentColor = new SKColor(mediaColor.R, mediaColor.G, mediaColor.B, mediaColor.A);
            }

            var occupancySeries = new ISeries[]
            {
                new PieSeries<ObservableValue>
                {
                    Values = new ObservableValue[] { _occupancyValue },
                    InnerRadius = 0.94,
                    MaxRadialColumnWidth = 5,
                    Stroke = null,
                    Fill = new SolidColorPaint(accentColor)
                },
                new PieSeries<ObservableValue>
                {
                    Values = new ObservableValue[] { _remainingValue },
                    InnerRadius = 0.94,
                    MaxRadialColumnWidth = 5,
                    Stroke = null,
                    Fill = new SolidColorPaint(new SKColor(accentColor.Red, accentColor.Green, accentColor.Blue, 15)) // Minimal track visibility
                }
            };

            var trendSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = new ObservableCollection<double>(), // Will be populated below
                    Fill = new LinearGradientPaint(new SKColor[] { new SKColor(16, 185, 129, 30), SKColors.Transparent }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.SpringGreen) { StrokeThickness = 3 },
                    LineSmoothness = 0 
                }
            };

            // Populate sparkline with real hourly check-in data
            Axis[] xAxes = { new Axis { LabelsPaint = new SolidColorPaint(SKColors.LightGray), TextSize = 10 } };
            using (var sparkScope = _scopeFactory.CreateScope())
            {
                var dashboardService = sparkScope.ServiceProvider.GetRequiredService<IDashboardService>();
                var facilityId = _facilityContext.CurrentFacilityId;
                if (facilityId == Guid.Empty)
                {
                    _logger?.LogWarning("[GymHome] InitializeAsync trend fetch aborted: FacilityId is Guid.Empty.");
                    // This return needs to be handled carefully when extracting.
                    // For now, it means the rest of the method might proceed with default values for charts.
                    // If this is a critical failure, consider throwing or setting a flag.
                }
                else
                {
                    var hourlyTrend = await dashboardService.GetGymOccupancyTrendAsync(facilityId);
                    if (hourlyTrend != null && hourlyTrend.Count > 0)
                    {
                        var values = hourlyTrend.Select(pt => pt.Value ?? 0).ToArray();
                        var labels = hourlyTrend.Select(pt => pt.DateTime.ToString("HH:mm")).ToArray();
                        trendSeries = new ISeries[]
                        {
                            new LineSeries<double>
                            {
                                Values = new ObservableCollection<double>(values),
                                Fill = new LinearGradientPaint(new SKColor[] { new SKColor(16, 185, 129, 30), SKColors.Transparent }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                                GeometrySize = 0,
                                Stroke = new SolidColorPaint(SKColors.SpringGreen) { StrokeThickness = 3 },
                                LineSmoothness = 0
                            }
                        };
                        xAxes = new Axis[]
                        {
                            new Axis
                            {
                                Labels = labels,
                                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                                TextSize = 10
                            }
                        };
                    }
                }
            }

            var yAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                    TextSize = 12
                }
            };
            
            // Use the helper method for data loading to avoid duplication
            // But InitializeAsync also sets up charts, so we keep chart logic here
            await LoadDashboardStatsAsync();
            
            // Finalize capacity-aware progress after stats are loaded
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                UpdateOccupancy(OccupancyCount);
            });

            await LoadRecentActivityAsync();

            // 2. Batch UI Updates
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                // Clock: Only set if NOT running to prevent "snap-back" during refreshes
                if (_clockTimer == null || string.IsNullOrEmpty(CurrentTime))
                {
                    CurrentTime = timeStr;
                    CurrentDate = dateStr;
                }
                
                StartClock();

                // Charts
                OccupancySeries = occupancySeries;
                OccupancyTrendSeries = trendSeries; 
                XAxes = xAxes;
                YAxes = yAxes;

                OnPropertyChanged(nameof(OccupancySeries));
                OnPropertyChanged(nameof(OccupancyTrendSeries));
                OnPropertyChanged(nameof(XAxes));
                OnPropertyChanged(nameof(YAxes));

                OccupancySparklineData.Clear();
                RevenueSparklineData.Clear();
            });

            // 3. Dynamic Alerts & Telemetry
            await PopulateSystemAlertsAsync();
        }

        private async Task LoadRecentActivityAsync()
        {
            // FIX Bug #6: In Remote Mode, activity events are embedded in the cloud snapshot
            // that was already fetched by LoadDashboardStatsAsync. Reading local SQLite here
            // would show stale on-premises data that doesn't belong to the remote viewer's context.
            if (_sessionManager.IsRemoteMode)
            {
                _logger?.LogDebug("[GymHome] Remote Mode: Skipping local SQLite activity stream load.");
                return;
            }

            try
            {
                // FIX: Use a fresh scope to resolve history providers.
                // This prevents the 'Captive Dependency' where a Singleton ViewModel
                // uses a Single Scoped DbContext concurrently across multiple threads.
                using var scope = _scopeFactory.CreateScope();
                var scopedProviders = scope.ServiceProvider.GetServices<Management.Application.Interfaces.App.IHistoryProvider>();

                var segmentName = _facilityContext.CurrentFacility.ToString();
                var provider = scopedProviders.FirstOrDefault(p => p.SegmentName == segmentName);
                if (provider == null) return;

                // Use a 24h window for activity stream
                var recentEvents = await provider.GetHistoryAsync(_facilityContext.CurrentFacilityId, DateTime.UtcNow.AddHours(-24), DateTime.UtcNow);

                var dashboardTasks = recentEvents.Take(50).Select(e => 
                {
                    var icon = e.Type switch
                    {
                        HistoryEventType.Access => e.IsSuccessful ? "✅" : "❌",
                        HistoryEventType.Payment => "🛒",
                        HistoryEventType.Sale => "🛒",
                        HistoryEventType.Order => "🛒",
                        _ => "✨"
                    };

                    var initials = e.Type switch
                    {
                        HistoryEventType.Access => new string((e.Title ?? "??").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0]).Take(2).ToArray()).ToUpper(),
                        HistoryEventType.Payment => "$$",
                        HistoryEventType.Sale => "$$",
                        HistoryEventType.Order => "$$",
                        _ => "??"
                    };

                    var titleKey = e.TitleLocalizationKey;
                    if (string.IsNullOrEmpty(titleKey) && e.Type == HistoryEventType.Sale && !string.IsNullOrEmpty(e.Title))
                    {
                        if (e.Title.Contains("Retail")) titleKey = "Terminology.Home.Activity.SaleRetail";
                        else if (e.Title.Contains("Membership")) titleKey = "Terminology.Home.Activity.SaleMembership";
                    }

                    var resolvedTitle = !string.IsNullOrEmpty(titleKey)
                        ? string.Format(System.Windows.Application.Current.TryFindResource(titleKey) as string ?? GetResource(titleKey, e.Title), e.TitleLocalizationArgs ?? Array.Empty<object>())
                        : e.Title;

                    var resolvedDetails = !string.IsNullOrEmpty(e.DetailsLocalizationKey)
                        ? string.Format(System.Windows.Application.Current.TryFindResource(e.DetailsLocalizationKey) as string ?? GetResource(e.DetailsLocalizationKey, e.DetailsLocalizationKey), e.DetailsLocalizationArgs ?? Array.Empty<object>())
                        : e.Details;

                    var subtitle = e.Amount.HasValue && e.Amount > 0 
                        ? $"{e.Amount:N0} {GetResource("Terminology.Dashboard.Activity.CurrencySuffix", "DA")} - {resolvedDetails}"
                        : resolvedDetails;

                    return new ActivityLogItem(resolvedTitle, subtitle, icon, initials)
                    {
                        Timestamp = e.Timestamp.ToLocalTime().ToString("HH:mm"),
                        SortDate = e.Timestamp
                    };
                }).ToList();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Destructive update: Ensure UI perfectly matches history.
                    // This prunes undone/deleted sales and events for deleted members.
                    ActivityStream.Clear();
                    foreach (var item in dashboardTasks)
                    {
                        ActivityStream.Add(item);
                    }
                    IsActivityEmpty = !ActivityStream.Any();
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load gym activity stream from HistoryProvider");
            }
        }

        private async Task UpdateActiveAvatarsAsync(Guid facilityId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var operationService = scope.ServiceProvider.GetRequiredService<IGymOperationService>();
                var avatars = await operationService.GetPeopleInsideAvatarsAsync(facilityId);
                var avatarList = avatars.ToList();

                var palette = new[] { "#8B5CF6", "#06B6D4", "#F59E0B", "#10B981", "#EC4899", "#3B82F6" };
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ActiveAvatars.Clear();
                    var displayList = avatarList.Take(4).ToList();
                    
                    for (int i = 0; i < displayList.Count; i++)
                    {
                        var color = palette[i % palette.Length];
                        // Convert hex to Alpha-Dimmed (20% opacity = #33 or similar)
                        var dimColor = "#33" + color.Substring(1);

                        ActiveAvatars.Add(new ActiveMemberAvatarViewModel
                        {
                            FullName = displayList[i].FullName,
                            Initials = displayList[i].Initials,
                            ColorHex = color,
                            DimColorHex = dimColor,
                            OverlapMargin = i == 0 ? 0 : -10 // Overlap after first item
                        });
                    }

                    ExtraActiveCount = Math.Max(0, avatarList.Count - displayList.Count);
                    HasActiveAvatars = avatarList.Any();
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update active avatars");
            }
        }

        /// <summary>Recomputes revenue progress bar value (0–1).</summary>
        private void UpdateRevenueProgress()
        {
            RevenueTodayProgress = DailyRevenueTarget > 0
                ? Math.Min(1.0, (double)RevenueToday / (double)DailyRevenueTarget)
                : 0;
        }

        /// <summary>Recomputes the expiring-soon ratio text and percentage.</summary>
        private void UpdateExpiringSoon()
        {
            if (ActiveMembersTotal > 0)
            {
                var format = GetResource("Terminology.Dashboard.Stat.FromActive", "from {0} active");
                ExpiringSoonRatioText = string.Format(format, ActiveMembersTotal);
                ExpiringSoonPct = Math.Round((double)ExpiringSoonCount / ActiveMembersTotal * 100, 1);
                IsHighExpiry = ExpiringSoonPct > 20;
            }
            else
            {
                ExpiringSoonRatioText = string.Empty;
                ExpiringSoonPct = 0;
                IsHighExpiry = false;
            }
        }

        /// <summary>Computes the active-members delta vs yesterday.</summary>
        private void UpdateMembersDelta(int yesterday)
        {
            if (yesterday <= 0)
            {
                HasMembersTrend = false;
                ActiveMembersDeltaText = string.Empty;
                return;
            }

            var delta = ActiveMembersTotal - yesterday;
            IsMembersTrendPositive = delta >= 0;
            var arrow = delta >= 0 ? "▲" : "▼";
            var vsYesterday = GetResource("Terminology.Dashboard.Stat.VsYesterday", "vs yesterday");
            ActiveMembersDeltaText = delta == 0
                ? vsYesterday
                : $"{arrow} {(delta >= 0 ? "+" : "")}{Math.Abs(delta)} {vsYesterday}";
            HasMembersTrend = true;
        }

        private void UpdateOccupancy(int count, int lastHourCount = -1)
        {
            OccupancyCount = count;
            
            // Update Ring Progress (don't exceed 100% physically, use overflow flag for visuals)
            _occupancyValue.Value = Math.Min(count, MaxCapacity);
            _remainingValue.Value = Math.Max(0, MaxCapacity - count);
            OccupancyPercentage = (count / (double)MaxCapacity) * 100;
            
            // Toggle Overflow State (triggers UI glow)
            IsOccupancyOverflow = count >= MaxCapacity;

            // Compute trend text vs last hour
            if (lastHourCount < 0)
            {
                // No data yet - hide trend
                OccupancyTrendText = string.Empty;
                HasOccupancyTrend = false;
            }
            else if (lastHourCount == 0)
            {
                // Avoid divide-by-zero: just show absolute delta
                var delta = count;
                IsTrendPositive = delta >= 0;
                var arrow = IsTrendPositive ? "▲" : "▼";
                var vsLastHour = GetResource("Terminology.Dashboard.Stat.VsLastHour", "vs last hour");
                OccupancyTrendText = delta == 0 ? vsLastHour : $"{arrow} {Math.Abs(delta)} {vsLastHour}";
                HasOccupancyTrend = true;
            }
            else
            {
                var percentDelta = (int)Math.Round(((double)(count - lastHourCount) / lastHourCount) * 100);
                IsTrendPositive = percentDelta >= 0;
                var arrow = IsTrendPositive ? "▲" : "▼";
                var vsLastHour = GetResource("Terminology.Dashboard.Stat.VsLastHour", "vs last hour");
                OccupancyTrendText = $"{arrow} {Math.Abs(percentDelta)}% {vsLastHour}";
                HasOccupancyTrend = true;
            }
        }

        [RelayCommand]
        public async Task ScanAsync()
        {
            if (_sessionManager.IsRemoteMode || string.IsNullOrWhiteSpace(ScanInput)) return;

            await ExecuteSafeAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var facilityId = _facilityContext.CurrentFacilityId;
                if (facilityId == Guid.Empty)
                {
                    _logger?.LogWarning("[GymHome] LoadDashboardStatsAsync aborted: FacilityId is Guid.Empty.");
                    return;
                }
                var operationService = scope.ServiceProvider.GetRequiredService<IGymOperationService>(); // Resolve service
                
                var result = await operationService.ProcessScanAsync(ScanInput, facilityId);
                
                // Show high-fidelity popup
                await _dialogService.ShowCustomDialogAsync<RfidAccessControlViewModel>(result);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => 
                {
                    
                    if (result.Status == AccessResult.Granted)
                    {
                        IsScanSuccessful = true;
                        EnvironmentState = "Success";
                        // Reset trigger
                        await Task.Delay(2000);
                        IsScanSuccessful = false;
                        EnvironmentState = "Normal";
                    }
                    else
                    {
                        IsScanError = true;
                        EnvironmentState = "Error";
                        // Reset trigger
                        await Task.Delay(2000);
                        IsScanError = false;
                        EnvironmentState = "Normal";
                    }

                    // Refresh stats using context-safe call
                    using (var refreshScope = _scopeFactory.CreateScope()) // Create new scope for refresh
                    {
                        var refreshService = refreshScope.ServiceProvider.GetRequiredService<IGymOperationService>(); // Resolve service
                        var stats = await refreshService.GetDailyStatsAsync(facilityId);
                        UpdateOccupancy(stats.OccupancyCount, stats.OccupancyLastHour);
                    }
                    
                    ScanInput = string.Empty;
                });
            }, "Member scan failed.");
        }

        [RelayCommand]
        public async Task ProcessWalkInAsync()
        {
            if (_sessionManager.IsRemoteMode) return;
            await _dialogService.ShowCustomDialogAsync<WalkInConfirmationViewModel>();
        }

        [RelayCommand]
        public async Task SellItemAsync()
        {
            if (_sessionManager.IsRemoteMode) return;
            await _dialogService.ShowCustomDialogAsync<QuickSaleViewModel>();
        }

        [RelayCommand]
        public async Task RegisterMemberAsync()
        {
            if (_sessionManager.IsRemoteMode) return;
            await _dialogService.ShowCustomDialogAsync<QuickRegistrationViewModel>();
        }

        [RelayCommand]
        public async Task OpenMultiSaleCartAsync()
        {
            if (_sessionManager.IsRemoteMode) return;
            await _dialogService.ShowCustomDialogAsync<MultiSaleCartViewModel>();
        }

        [RelayCommand]
        public async Task SimulateScanAsync()
        {
            await _accessEventService.SimulateScanAsync(_facilityContext.CurrentFacilityId);
        }

        [RelayCommand]
        public async Task RefreshDashboardAsync()
        {
            await LoadDashboardStatsAsync();
        }

        public void Receive(FacilityActionCompletedMessage message)
        {
             if (message.Value != _facilityContext.CurrentFacilityId) return;
             
             // OPTIMIZATION: Combine optimistic log entry with data refresh.
             // We do the UI log insertion immediately, but it now acts as a suppression
             // for the background refresh to avoid redundant work.
             _ = ExecuteHandleAsync(message);
        }
        
        public void Receive(RefreshRequiredMessage<Sale> message) => HandleRefresh(message.Value);
        public void Receive(RefreshRequiredMessage<Member> message) => HandleRefresh(message.Value);
        public void Receive(RefreshRequiredMessage<PayrollEntry> message) => HandleRefresh(message.Value);
        public void Receive(RefreshRequiredMessage<InventoryPurchaseDto> message) => HandleRefresh(message.Value);
        public void Receive(RefreshRequiredMessage<Registration> message) => HandleRefresh(message.Value);

        private void HandleRefresh(Guid facilityId)
        {
            if (facilityId != _facilityContext.CurrentFacilityId) return;

            // Ensure a navigation-back after this refresh will re-fetch instead of returning
            // early from the _initialized && !_isDirty guard.
            _isDirty = true;

            // Cancel any in-flight debounce and start a fresh 300ms window.
            // This coalesces rapid-fire messages (e.g. a checkout publishes
            // FacilityActionCompletedMessage + RefreshRequiredMessage<Sale> within
            // the same millisecond) into a single DB round-trip.
            _refreshDebounceCts?.Cancel();
            _refreshDebounceCts = new CancellationTokenSource();
            var token = _refreshDebounceCts.Token;

            // Fix 2: Use Task.Run — NOT Dispatcher.InvokeAsync — so the DB queries
            // run entirely off the UI thread. LoadDashboardStatsAsync and
            // LoadRecentActivityAsync already marshal their final property updates
            // back to the Dispatcher internally via their own InvokeAsync calls.
            _ = Task.Run(async () =>
            {
                try
                {
                    // Debounce + small resiliency buffer to ensure SQLite file context sync
                    await Task.Delay(400, token); 
                    if (token.IsCancellationRequested || IsDisposed) return;
                    
                    // Use silent refresh to avoid flickering the UI with skeleton loaders
                    await InitializeAsync(silent: true);
                }
                catch (TaskCanceledException) { /* debounced away — normal */ }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during background refresh");
                }
            }, token);
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (IsDisposed || !ShouldRefreshOnSync()) return;
            
            // Trigger a silent refresh in the background
            _ = Task.Run(async () => 
            {
                if (IsDisposed) return;
                _logger?.LogInformation("[GymHome] Sync completed, triggering refresh.");
                await InitializeAsync(silent: true);
            });
        }

        private async Task ExecuteHandleAsync(FacilityActionCompletedMessage message)
        {
            if (IsDisposed) return;
            try
            {
                if (message.ActionType == "MemberDelete" || message.ActionType == "MemberRestore")
                {
                    _logger?.LogInformation("[GymHome] Skipping dashboard log for {ActionType} as requested.", message.ActionType);
                    return;
                }

                // 1. Calculate / Prepare Data (Off-UI Thread)
                string icon = message.ActionType switch
                {
                    "Walk-In" or "WalkIn" => "🚶",
                    "Sale" or "QuickSale" => "🛒",
                    "Registration" => "👤",
                    "Access" => message.Message.Contains("Denied") ? "❌" : "✅",
                    _ => "✨"
                };

                string initials = message.ActionType switch
                {
                    "Walk-In" or "WalkIn" => "WG",
                    "Sale" or "QuickSale" => "$$",
                    "Registration" => "++",
                    "Access" => "IN",
                    _ => "??"
                };

                string statusKey = message.ActionType switch
                {
                    "Walk-In" or "WalkIn" => "Terminology.Home.Status.WalkIn",
                    "Sale" or "QuickSale" => "Terminology.Home.Status.Sale",
                    "Registration" => "Terminology.Home.Status.Registration",
                    "Access" => "Terminology.Home.Status.Access",
                    _ => "Terminology.Global.Success"
                };

                string status = System.Windows.Application.Current.TryFindResource(statusKey) as string ?? (message.ActionType == "Access" ? message.Message : message.ActionType);

                string displayName = message.DisplayName;
                if (message.ActionType == "Sale" || message.ActionType == "QuickSale")
                {
                    if (displayName.Contains("Retail")) displayName = System.Windows.Application.Current.TryFindResource("Terminology.Home.Activity.SaleRetail") as string ?? displayName;
                    else if (displayName.Contains("Membership")) displayName = System.Windows.Application.Current.TryFindResource("Terminology.Home.Activity.SaleMembership") as string ?? displayName;
                }

                var logItem = new ActivityLogItem(
                    displayName,
                    status,
                    icon,
                    initials,
                    statusKey);

                // 2. Fetch Latest Stats (Async, Non-Blocking, Throttled)
                DailyStatsDto? stats = null;
                DashboardSummaryDto? summary = null;
                await ExecuteBackgroundAsync(async () =>
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var operationService = scope.ServiceProvider.GetRequiredService<IGymOperationService>();
                        var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                        var facilityId = _facilityContext.CurrentFacilityId;
                        
                        stats = await operationService.GetDailyStatsAsync(facilityId);
                        summary = await dashboardService.GetSummaryAsync(facilityId);
                    }
                }, _refreshSemaphore);

                // 3. Update UI on Dispatcher
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // OPTIMISTIC UPDATE: Add the log item immediately
                    ActivityStream.Insert(0, logItem);
                    if (ActivityStream.Count > 50) ActivityStream.RemoveAt(ActivityStream.Count - 1);
                    IsActivityEmpty = !ActivityStream.Any();
                    
                    // Refresh avatars after a check-in
                    _ = UpdateActiveAvatarsAsync(_facilityContext.CurrentFacilityId);
                    
                    // CRITICAL FIX: Update ALL relevant cards immediately
                    if (stats != null)
                    {
                        UpdateOccupancy(stats.OccupancyCount, stats.OccupancyLastHour);
                        RevenueToday = stats.DailyCashTotal;
                    }

                    if (summary != null)
                    {
                        ActiveMembersTotal = summary.ActiveMembers;
                        ExpiringSoonCount = summary.ExpiringSoonCount;
                        PendingRegistrationsCount = summary.PendingRegistrationsCount;
                        DailyRevenueTarget = summary.DailyRevenueTarget > 0 ? summary.DailyRevenueTarget : 10_000m;
                        UpdateRevenueProgress();
                        UpdateExpiringSoon();
                        UpdateMembersDelta(summary.ActiveMembersYesterday);
                        PtUpsellRate = summary.PtUpsellRate ?? new KpiMetricDto();

                        // Map demographics to chart series
                        if (summary.MemberDemographics != null && summary.MemberDemographics.Any())
                        {
                            var primaryColor = (SKColor)System.Windows.Application.Current.Resources["Color.Brand.Primary"];
                            var accentColor = (SKColor)System.Windows.Application.Current.Resources["Color.Brand.Accent"];
                            
                            DemographicSeries = summary.MemberDemographics.Select((d, index) => new PieSeries<int>
                            {
                                Name = !string.IsNullOrEmpty(d.Source) ? GetTerm(d.Source) : "Unknown",
                                Values = new[] { d.Count },
                                InnerRadius = 40,
                                Stroke = null,
                                Fill = new SolidColorPaint(index % 2 == 0 ? primaryColor : accentColor)
                            }).ToArray();
                            OnPropertyChanged(nameof(DemographicSeries));
                        }
                    }
                });

                // Trigger a priority cloud snapshot push so Remote Viewers see this update immediately.
                if (!_sessionManager.IsRemoteMode)
                {
                    _messenger.Send(new SyncRequestedMessage(_facilityContext.CurrentFacilityId));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling FacilityActionCompletedMessage");
            }
        }
        public void ResetState()
        {
            // Fix 3: Do NOT set IsActive=false here. IsActive reflects whether this screen is
            // currently visible. ResetState only clears transient data; the Singleton ViewModel
            // is immediately re-displayed after login, so IsActive should remain true.
            _logger?.LogInformation("Resetting state for GymHomeViewModel");
            
            // 1. Clear Data Collections
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ActivityStream.Clear();
                OccupancySparklineData.Clear();
                RevenueSparklineData.Clear();
                _occupancyValue.Value = 0;
                _remainingValue.Value = 100;
            });

            // 2. Reset Metrics
            OccupancyCount = 0;
            RevenueToday = 0;
            RevenueTodayProgress = 0;
            ActiveMembersTotal = 0;
            ExpiringSoonCount = 0;
            ExpiringSoonRatioText = string.Empty;
            ExpiringSoonPct = 0;
            PendingRegistrationsCount = 0;
            OccupancyPercentage = 0;
            HasMembersTrend = false;
            ActiveMembersDeltaText = string.Empty;
            
            // 3. Reset Status Flags
            IsScanSuccessful = false;
            IsScanError = false;
            EnvironmentState = "Normal";
            ScanInput = string.Empty;

            // 4. Force Reload Trigger (Optional, acts as "Invalidate")
            // Next time the view is navigated to, it should reload.
            // Or rely on MainViewModel's orchestration to re-initialize if needed.
            // For now, clearing data prevents "Stale Data" from showing up.
        }

        private void OnFacilityChanged(Management.Domain.Enums.FacilityType type)
        {
            if (IsDisposed) return;
            _logger?.LogInformation("[GymHome] FacilityChanged event received ({Type}). Reloading data.", type);
            var newFacilityId = _facilityContext.CurrentFacilityId;
            
            if (newFacilityId != Guid.Empty)
            {
                // Fix 2: Mark active here so ShouldRefreshOnSync() is unblocked for sync-triggered
                // refreshes that arrive while the home screen is live.
                IsActive = true;
                _logger?.LogInformation("[GymHome] FacilityId resolved ({Id}). Reloading stats.", newFacilityId);
                System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (IsDisposed) return;
                    // Reset initialized flag to force a full fresh load
                    _initialized = false;
                    await InitializeAsync(silent: false);
                });
            }
        }

        [RelayCommand]
        public void SetSidebarMode(string modeStr)
        {
            if (Enum.TryParse<SidebarMode>(modeStr, out var mode))
            {
                CurrentSidebarMode = mode;
            }
        }

        [RelayCommand]
        public void NextGuideTip()
        {
            if (GuideTips.Count == 0) return;
            CurrentGuideIndex = (CurrentGuideIndex + 1) % GuideTips.Count;
            CurrentGuide = GuideTips[CurrentGuideIndex];
            ResetCarouselTimer();
        }

        [RelayCommand]
        public void PreviousGuideTip()
        {
            if (GuideTips.Count == 0) return;
            CurrentGuideIndex = (CurrentGuideIndex - 1 + GuideTips.Count) % GuideTips.Count;
            CurrentGuide = GuideTips[CurrentGuideIndex];
            ResetCarouselTimer();
        }

        [RelayCommand]
        public void SelectGuideTip(int index)
        {
            if (index < 0 || index >= GuideTips.Count) return;
            CurrentGuideIndex = index;
            CurrentGuide = GuideTips[CurrentGuideIndex];
            ResetCarouselTimer();
        }

        private void InitializeGuideTips()
        {
            GuideTips.Clear();
            
            GuideTips.Add(new GuideTipViewModel { StepNumber = 1, Title = GetResource("Terminology.Dashboard.Guide.Welcome.Title", "Welcome to the Dashboard"), Description = GetResource("Terminology.Dashboard.Guide.Welcome.Description", "This is your central command center. Stay on top of memberships, sales, and access control seamlessly.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 2, Title = GetResource("Terminology.Dashboard.Guide.WalkIn.Title", "Record a walk-in visit"), Description = GetResource("Terminology.Dashboard.Guide.WalkIn.Description", "Track daily visitors who aren't members using the Walk-in quick action module.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 3, Title = GetResource("Terminology.Dashboard.Guide.QuickSale.Title", "Quick Point of Sale"), Description = GetResource("Terminology.Dashboard.Guide.QuickSale.Description", "Process water, towels, and supplements instantly using the Quick Sale flow.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 4, Title = GetResource("Terminology.Dashboard.Guide.ExpiringSoon.Title", "Expiring Soon Alerts"), Description = GetResource("Terminology.Dashboard.Guide.ExpiringSoon.Description", "Monitor the KPI cards to proactively engage members before their subscription lapses.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 5, Title = GetResource("Terminology.Dashboard.Guide.Hardware.Title", "Hardware Telemetry"), Description = GetResource("Terminology.Dashboard.Guide.Hardware.Description", "Check the bottom footer to ensure your barcode scanners and receipt printers are online.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 6, Title = GetResource("Terminology.Dashboard.Guide.MultiSale.Title", "Multi-Item Sales"), Description = GetResource("Terminology.Dashboard.Guide.MultiSale.Description", "Handling a large checkout? Use the Multi-Sale Cart to bundle items securely.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 7, Title = GetResource("Terminology.Dashboard.Guide.Overflow.Title", "Occupancy Overflow"), Description = GetResource("Terminology.Dashboard.Guide.Overflow.Description", "When the gym hits Max Capacity, the occupancy ring will glow to alert front-desk staff.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 8, Title = GetResource("Terminology.Dashboard.Guide.Activity.Title", "Activity Stream"), Description = GetResource("Terminology.Dashboard.Guide.Activity.Description", "Watch the real-time event log update automatically as people scan in or make purchases.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 9, Title = GetResource("Terminology.Dashboard.Guide.NewMember.Title", "Creating New Members"), Description = GetResource("Terminology.Dashboard.Guide.NewMember.Description", "Use the Create Member action to rapidly enroll walk-ins directly from the Home screen.") });
            GuideTips.Add(new GuideTipViewModel { StepNumber = 10, Title = GetResource("Terminology.Dashboard.Guide.EndOfShift.Title", "End of Shift Protocol"), Description = GetResource("Terminology.Dashboard.Guide.EndOfShift.Description", "Verify the expected Daily Cash Total matches your physical till before logging out.") });

            if (GuideTips.Any())
                CurrentGuide = GuideTips[0];
            CurrentGuideIndex = 0;
        }

        private async Task PopulateSystemAlertsAsync()
        {
            if (_facilityContext.CurrentFacilityId == Guid.Empty) return;

            var alerts = new List<string>();

            using (var scope = _scopeFactory.CreateScope())
            {
                var memberService = scope.ServiceProvider.GetRequiredService<IMemberService>();
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                var facilityId = _facilityContext.CurrentFacilityId;

                // 1. Connectivity Check
                try
                {
                    var diag = await _diagnosticService.TestSupabaseConnectivityAsync();
                    if (!diag.IsSuccess)
                    {
                        alerts.Add(GetResource("Terminology.Dashboard.Alert.Connectivity", "⚠️ Network latency detected or local database is in offline mode."));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Alerts: Connectivity check failed.");
                }

                // 2. Low Stock Alerts
                try
                {
                    var productsResult = await productService.GetLowStockProductsAsync(facilityId);
                    if (productsResult.IsSuccess && productsResult.Value.Any())
                    {
                        foreach (var p in productsResult.Value.Take(3))
                        {
                            var alertFormat = GetResource("Terminology.Dashboard.Alert.LowStock", "⚠️ Inventory warning: '{0}' is below minimum threshold.");
                            alerts.Add(string.Format(alertFormat, p.Name));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Alerts: Low stock check failed.");
                }

                // 3. Expired Membership Alerts
                try
                {
                    var membersResult = await memberService.GetRecentlyExpiredMembersAsync(facilityId, 7);
                    if (membersResult.IsSuccess && membersResult.Value.Any())
                    {
                        foreach (var m in membersResult.Value.Take(3))
                        {
                            var days = (DateTime.UtcNow - m.ExpirationDate).Days;
                            var dayStr = days <= 0 ? "today" : $"{days} days ago";
                            var alertFormat = GetResource("Terminology.Dashboard.Alert.Expired", "🔴 Member {0} subscription expired {1}.");
                            alerts.Add(string.Format(alertFormat, m.FullName, dayStr));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Alerts: Expired membership check failed.");
                }
            }

            // Update on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SystemAlerts.Clear();
                foreach (var alert in alerts)
                {
                    SystemAlerts.Add(alert);
                }

                if (!SystemAlerts.Any())
                {
                    SystemAlerts.Add(GetResource("Terminology.Dashboard.Alert.NoAlerts", "✅ No critical system alerts at this time."));
                }
            });
        }

        private void StartCarousel()
        {
            if (_carouselTimer != null) return;
            _carouselTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _carouselTimer.Tick += (s, e) => NextGuideTip();
            _carouselTimer.Start();
        }

        private void ResetCarouselTimer()
        {
            if (_carouselTimer != null)
            {
                _carouselTimer.Stop();
                _carouselTimer.Start();
            }
        }
    }
}
