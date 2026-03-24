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

namespace Management.Presentation.ViewModels.GymHome
{
    public partial class GymHomeViewModel : ViewModelBase, IFacilityHomeViewModel, IStateResettable, 
        IRecipient<FacilityActionCompletedMessage>,
        IRecipient<RefreshRequiredMessage<Sale>>,
        IRecipient<RefreshRequiredMessage<Member>>,
        IRecipient<RefreshRequiredMessage<Registration>>,
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
            ActivityStream = new ObservableRangeCollection<IActivityItem>();
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

               // FIX: Execute sequentially to prevent EF Core DbContext concurrency exceptions
               var stats = await operationService.GetDailyStatsAsync(facilityId);
               var summary = await dashboardService.GetSummaryAsync(facilityId);

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
        }

        private async Task LoadRecentActivityAsync()
        {
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

                    var resolvedTitle = !string.IsNullOrEmpty(e.TitleLocalizationKey)
                        ? string.Format(System.Windows.Application.Current.TryFindResource(e.TitleLocalizationKey) as string ?? GetResource(e.TitleLocalizationKey, e.TitleLocalizationKey), e.TitleLocalizationArgs ?? Array.Empty<object>())
                        : e.Title;

                    var resolvedDetails = !string.IsNullOrEmpty(e.DetailsLocalizationKey)
                        ? string.Format(System.Windows.Application.Current.TryFindResource(e.DetailsLocalizationKey) as string ?? GetResource(e.DetailsLocalizationKey, e.DetailsLocalizationKey), e.DetailsLocalizationArgs ?? Array.Empty<object>())
                        : e.Details;

                    var subtitle = e.Amount.HasValue && e.Amount > 0 
                        ? $"{e.Amount:N0} DA - {resolvedDetails}"
                        : resolvedDetails;

                    return new ActivityLogItem(resolvedTitle, subtitle, icon, initials)
                    {
                        Timestamp = e.Timestamp.ToLocalTime().ToString("HH:mm"),
                        SortDate = e.Timestamp
                    };
                }).ToList();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Non-destructive update: Add missing items instead of wiping everything
                    var existingKeys = new HashSet<string>(ActivityStream.OfType<ActivityLogItem>().Select(a => $"{a.SortDate:O}_{a.Title}"));
                    var newItems = dashboardTasks.Where(a => !existingKeys.Contains($"{a.SortDate:O}_{a.Title}")).ToList();

                    if (newItems.Any())
                    {
                        foreach (var item in newItems.OrderBy(a => a.SortDate))
                        {
                            ActivityStream.Insert(0, item);
                        }
                        
                        // Limit to 50
                        while (ActivityStream.Count > 50) ActivityStream.RemoveAt(ActivityStream.Count - 1);
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

                var logItem = new ActivityLogItem(
                    message.DisplayName,
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
                    }
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
                    // Reset initialized flag to force a full fresh load
                    _initialized = false;
                    await InitializeAsync(silent: false);
                });
            }
        }

    }
}

