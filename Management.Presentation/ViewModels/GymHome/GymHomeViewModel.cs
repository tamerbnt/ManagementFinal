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

namespace Management.Presentation.ViewModels.GymHome
{
    public partial class GymHomeViewModel : ViewModelBase, IFacilityHomeViewModel, IStateResettable, 
        IRecipient<FacilityActionCompletedMessage>,
        IRecipient<RefreshRequiredMessage<Sale>>,
        IRecipient<RefreshRequiredMessage<Member>>,
        IRecipient<RefreshRequiredMessage<PayrollEntry>>,
        IRecipient<RefreshRequiredMessage<InventoryPurchaseDto>>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Management.Domain.Services.IDialogService _dialogService;
        private readonly SessionManager _sessionManager;
        private readonly IFacilityContextService _facilityContext;
        private readonly ILocalizationService _localizationService;
        private readonly IAccessEventService _accessEventService;
        private readonly ISyncService _syncService;
        private readonly IEnumerable<IHistoryProvider> _historyProviders;
        private readonly LiveChartsCore.Defaults.ObservableValue _occupancyValue = new(0);
        private readonly LiveChartsCore.Defaults.ObservableValue _remainingValue = new(100);

        [ObservableProperty]
        private ObservableCollection<IActivityItem> _activityStream = new();

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
        private string _occupancyTrendText = string.Empty;

        [ObservableProperty]
        private bool _isTrendPositive = true;

        [ObservableProperty]
        private bool _hasOccupancyTrend = false;

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

        private DispatcherTimer? _clockTimer;
        // Debounce token for HandleRefresh — coalesces rapid-fire RefreshRequiredMessages
        // (e.g. Sale + FacilityAction arriving within the same checkout commit)
        // into a single DB round-trip 300ms later.
        private CancellationTokenSource? _refreshDebounceCts;
        public GymHomeViewModel(
            IServiceScopeFactory scopeFactory, 
            Management.Domain.Services.IDialogService dialogService,
            ILogger<GymHomeViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            SessionManager sessionManager,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            IAccessEventService accessEventService,
            IEnumerable<IHistoryProvider> historyProviders,
            ISyncService syncService) : base(logger, diagnosticService, toastService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;
            _sessionManager = sessionManager;
            _facilityContext = facilityContext;
            _localizationService = localizationService;
            _accessEventService = accessEventService;
            _historyProviders = historyProviders;
            _syncService = syncService;

            _syncService.SyncCompleted += OnSyncCompleted;
            _facilityContext.FacilityChanged += OnFacilityChanged;

            _localizationService.LanguageChanged += (s, e) => 
            {
                CurrentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy", _localizationService.CurrentCulture);
            };
            
            // Lightweight initialization ONLY
            ActivityStream = new ObservableCollection<IActivityItem>();
            OccupancySeries = Array.Empty<ISeries>();
            OccupancyTrendSeries = Array.Empty<ISeries>();
            XAxes = Array.Empty<Axis>();
            YAxes = Array.Empty<Axis>();

            
            // Register for Messenger updates
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.RegisterAll(this);

            // Initial load - Clock only, stats deferred to Loaded event
            StartClock();
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
                >= 5 and < 12  => GetResource("Terminology.Greeting.Morning",  "Good Morning"),
                >= 12 and < 18 => GetResource("Terminology.Greeting.Afternoon", "Good Afternoon"),
                _              => GetResource("Terminology.Greeting.Evening",   "Good Evening")
            };

            var name = _sessionManager?.CurrentUser?.FullName?.Split(' ').FirstOrDefault() ?? string.Empty;
            GreetingText = string.IsNullOrEmpty(name) ? salutation : $"{salutation}, {name}";
        }

        private static string GetResource(string key, string fallback)
        {
            return System.Windows.Application.Current.TryFindResource(key) as string ?? fallback;
        }

        private async Task LoadDashboardStatsAsync()
        {
           try
           {
               using var scope = _scopeFactory.CreateScope(); // Create scope
               var operationService = scope.ServiceProvider.GetRequiredService<IGymOperationService>(); // Resolve service
               var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>(); // Resolve service

               var facilityId = _facilityContext.CurrentFacilityId;
               if (facilityId == Guid.Empty)
               {
                   _logger?.LogWarning("[GymHome] LoadDashboardStatsAsync aborted: FacilityId is Guid.Empty.");
                   return;
               }
               var statsTask = operationService.GetDailyStatsAsync(facilityId);
               var summaryTask = dashboardService.GetSummaryAsync(facilityId);
               
               await Task.WhenAll(statsTask, summaryTask);
               
               var stats = await statsTask;
               var summary = await summaryTask;

               await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
               {
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

        public async Task InitializeAsync()
        {
            IsActive = true;
            await ExecuteLoadingAsync(async () =>
            {
                // SAFETY: No hardcoded Task.Delay here. Initialization triggered by Loaded event.
                // 1. Prepare Data & Visuals on Background Thread
                var now = DateTime.Now;
                var timeStr = now.ToString("HH:mm:ss");
                var dateStr = now.ToString("dddd, MMMM dd, yyyy");

                var occupancySeries = new ISeries[]
                {
                    new PieSeries<ObservableValue>
                    {
                        Values = new ObservableValue[] { _occupancyValue },
                        InnerRadius = 60,
                        MaxRadialColumnWidth = 20,
                        Stroke = null,
                        Fill = new SolidColorPaint(SKColors.DeepSkyBlue)
                    },
                    new PieSeries<ObservableValue>
                    {
                        Values = new ObservableValue[] { _remainingValue },
                        InnerRadius = 60,
                        MaxRadialColumnWidth = 20,
                        Stroke = null,
                        Fill = new SolidColorPaint(new SKColor(200, 200, 200, 30))
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
                        return;
                    }
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
                await LoadRecentActivityAsync();

                // 2. Batch UI Updates in a SINGLE Dispatcher Call
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    // Clock
                    CurrentTime = timeStr;
                    CurrentDate = dateStr;
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

                     // Micro-metrics Sparkline data (Mocked) -> Cleared
                     OccupancySparklineData.Clear();
                     RevenueSparklineData.Clear();
                 });
             });
         }

        private async Task LoadRecentActivityAsync()
        {
            try
            {
                var segmentName = _facilityContext.CurrentFacility.ToString();
                var provider = _historyProviders.FirstOrDefault(p => p.SegmentName == segmentName);
                if (provider == null) return;

                // Fix 3: Use a 24h window for triggered refreshes to keep the query fast.
                // The initial full load (InitializeAsync) uses the same method, so 24h
                // is a reasonable window — events older than a day rarely appear in the
                // "Recent Activity" feed anyway.
                var recentEvents = await provider.GetHistoryAsync(_facilityContext.CurrentFacilityId, DateTime.UtcNow.AddHours(-24), DateTime.UtcNow);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ActivityStream.Clear();
                    foreach (var e in recentEvents.Take(50))
                    {
                        var icon = e.Type switch
                        {
                            HistoryEventType.Access => e.IsSuccessful ? "✅" : "❌",
                            HistoryEventType.Payment => "🛒",
                            HistoryEventType.Order => "🛒",
                            _ => "✨"
                        };

                        var initials = e.Type switch
                        {
                            HistoryEventType.Access => new string((e.Title ?? "??").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0]).Take(2).ToArray()).ToUpper(),
                            HistoryEventType.Payment => "$$",
                            HistoryEventType.Order => "$$",
                            _ => "??"
                        };

                        var resolvedTitle = !string.IsNullOrEmpty(e.TitleLocalizationKey)
                            ? string.Format(System.Windows.Application.Current.TryFindResource(e.TitleLocalizationKey) as string ?? GetResource(e.TitleLocalizationKey, e.TitleLocalizationKey), e.TitleLocalizationArgs ?? Array.Empty<object>())
                            : e.Title;

                        var resolvedDetails = !string.IsNullOrEmpty(e.DetailsLocalizationKey)
                            ? string.Format(System.Windows.Application.Current.TryFindResource(e.DetailsLocalizationKey) as string ?? GetResource(e.DetailsLocalizationKey, e.DetailsLocalizationKey), e.DetailsLocalizationArgs ?? Array.Empty<object>())
                            : e.Details;

                        var subtitle = e.Amount.HasValue && e.Amount > 0 
                            ? $"{e.Amount:N0} DA - {resolvedDetails}"
                            : resolvedDetails;

                        ActivityStream.Add(new ActivityLogItem(resolvedTitle, subtitle, icon, initials)
                        {
                            Timestamp = e.Timestamp.ToLocalTime().ToString("HH:mm"),
                            SortDate = e.Timestamp
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load gym activity stream from HistoryProvider");
            }
        }

        private void UpdateOccupancy(int count, int lastHourCount = -1)
        {
            OccupancyCount = count;
            _occupancyValue.Value = count;
            _remainingValue.Value = Math.Max(0, 100 - count);
            OccupancyPercentage = (count / 100.0) * 100;

            // Compute trend text vs last hour
            if (lastHourCount < 0)
            {
                // No data yet â€” hide trend
                OccupancyTrendText = string.Empty;
                HasOccupancyTrend = false;
            }
            else if (lastHourCount == 0)
            {
                // Avoid divide-by-zero: just show absolute delta
                var delta = count;
                IsTrendPositive = delta >= 0;
                var arrow = IsTrendPositive ? "â–²" : "â–¼";
                var vsLastHour = GetResource("Terminology.Dashboard.Stat.VsLastHour", "vs last hour");
                OccupancyTrendText = delta == 0 ? vsLastHour : $"{arrow} {Math.Abs(delta)} {vsLastHour}";
                HasOccupancyTrend = true;
            }
            else
            {
                var percentDelta = (int)Math.Round(((double)(count - lastHourCount) / lastHourCount) * 100);
                IsTrendPositive = percentDelta >= 0;
                var arrow = IsTrendPositive ? "â–²" : "â–¼";
                var vsLastHour = GetResource("Terminology.Dashboard.Stat.VsLastHour", "vs last hour");
                OccupancyTrendText = $"{arrow} {Math.Abs(percentDelta)}% {vsLastHour}";
                HasOccupancyTrend = true;
            }
        }

        [RelayCommand]
        public async Task ScanAsync()
        {
            if (string.IsNullOrWhiteSpace(ScanInput)) return;

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
             
             // Fire-and-forget to prevent blocking the publisher
             _ = ExecuteHandleAsync(message);
        }
        
        public void Receive(RefreshRequiredMessage<Sale> message) => HandleRefresh(message.Value);
        public void Receive(RefreshRequiredMessage<Member> message) => HandleRefresh(message.Value);
        public void Receive(RefreshRequiredMessage<PayrollEntry> message) => HandleRefresh(message.Value);
        public void Receive(RefreshRequiredMessage<InventoryPurchaseDto> message) => HandleRefresh(message.Value);

        private void HandleRefresh(Guid facilityId)
        {
            if (facilityId != _facilityContext.CurrentFacilityId) return;

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
                    await Task.Delay(300, token); // debounce window
                    if (token.IsCancellationRequested || IsDisposed) return;
                    await LoadDashboardStatsAsync();
                    if (token.IsCancellationRequested || IsDisposed) return;
                    await LoadRecentActivityAsync();
                }
                catch (TaskCanceledException) { /* debounced away — normal */ }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[GymHome] HandleRefresh background task failed");
                }
            }, token);
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (IsDisposed || !ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[GymHome] Sync debounce passed, refreshing stats...");
                await LoadDashboardStatsAsync();
            });
        }

        private async Task ExecuteHandleAsync(FacilityActionCompletedMessage message)
        {
            if (IsDisposed) return;
            try
            {
                // 1. Calculate / Prepare Data (Off-UI Thread)
                string icon = message.ActionType switch
                {
                    "Walk-In" or "WalkIn" => "ðŸš¶",
                    "Sale" or "QuickSale" => "ðŸ›’",
                    "Registration" => "ðŸ‘¤",
                    _ => "âœ¨"
                };

                string initials = message.ActionType switch
                {
                    "Walk-In" or "WalkIn" => "WG",
                    "Sale" or "QuickSale" => "$$",
                    "Registration" => "++",
                    _ => "??"
                };

                string statusKey = message.ActionType switch
                {
                    "Walk-In" or "WalkIn" => "Terminology.Home.Status.WalkIn",
                    "Sale" or "QuickSale" => "Terminology.Home.Status.Sale",
                    "Registration" => "Terminology.Home.Status.Registration",
                    _ => "Terminology.Global.Success"
                };

                string status = System.Windows.Application.Current.TryFindResource(statusKey) as string ?? message.ActionType;

                var logItem = new ActivityLogItem(
                    message.DisplayName,
                    status,
                    icon,
                    initials,
                    statusKey);

                // 2. Fetch Latest Stats (Async, Non-Blocking)
                DailyStatsDto? stats = null;
                try
                {
                    using (var scope = _scopeFactory.CreateScope()) // Create scope
                    {
                        var operationService = scope.ServiceProvider.GetRequiredService<IGymOperationService>(); // Resolve service
                        stats = await operationService.GetDailyStatsAsync(_facilityContext.CurrentFacilityId);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to refresh stats in Handle");
                }

                // 3. Update UI on Dispatcher
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ActivityStream.Insert(0, logItem);
                    if (ActivityStream.Count > 50) ActivityStream.RemoveAt(ActivityStream.Count - 1);
                    
                    // Note: We don't fetch stats here anymore because the 
                    // RefreshRequiredMessage handler (which fires immediately after)
                    // will trigger a call to LoadDashboardStatsAsync for a full refresh.
                });
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
            ActiveMembersTotal = 0;
            ExpiringSoonCount = 0;
            PendingRegistrationsCount = 0;
            OccupancyPercentage = 0;
            
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
                    await LoadDashboardStatsAsync();
                    await LoadRecentActivityAsync();
                });
            }
        }

    }
}

