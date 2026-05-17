using System;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading;

using Management.Application.Services;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Presentation.ViewModels.Auth; // Fixed: Missing namespace for SplashOnboardingViewModel
using Management.Infrastructure.Services.Dashboard;
using Management.Infrastructure.Services.Dashboard.Aggregators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Linq;
using Microsoft.Data.Sqlite;

using MediatR;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Management.Application.Notifications;
using Microsoft.Extensions.Hosting;


using Management.Presentation.Stores;
using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Infrastructure.Integrations.Supabase.Models;
using Management.Infrastructure.Services;
using Management.Infrastructure.Services.Sync;
using Management.Domain.Services;
using Management.Domain.Models;
using Management.Domain.Models.Restaurant;
using Management.Infrastructure.Data;
using Management.Infrastructure.Hardware;
using Management.Infrastructure.Configuration;
using Management.Infrastructure.Repositories;
using Management.Infrastructure.Services;
using Management.Infrastructure.Workers;
using Management.Infrastructure.Services.Audio;
using Management.Presentation.Services;
using Management.Presentation.Services.State;
using Management.Presentation.Services.Application;
using Management.Presentation.ViewModels.Auth; // Fixed: Missing namespace for SplashOnboardingViewModel
// using Management.Presentation.Services.Restaurant; // Removed to avoid ambiguity with Application services
using Management.Presentation.Views.Salon; // Added
using Management.Presentation.Views.Auth; // Added for LoginView
using Management.Presentation.Services.Salon;
using Management.Presentation.Views.Shop;
using Management.Presentation.Services.Localization;
using Management.Presentation.Views.Settings;
using Management.Presentation.Views.FinanceAndStaff;
using Management.Presentation.ViewModels;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.ViewModels.History;
using Management.Presentation.ViewModels.Members;
using Management.Presentation.ViewModels.Registrations;
using Management.Presentation.ViewModels.Finance;
using Management.Presentation.ViewModels.Shop;
using Management.Presentation.ViewModels.Dashboard;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.ViewModels.Settings;
using Management.Presentation.ViewModels.Shared;
using Management.Presentation.ViewModels.Sync;
using Management.Presentation.ViewModels.Diagnostic;
using Management.Presentation.ViewModels.GymHome;
using Management.Presentation.ViewModels.Salon;
using Management.Presentation.ViewModels.Restaurant;
using Management.Presentation.ViewModels.Members;
using Management.Presentation.ViewModels.History;
using Management.Presentation.ViewModels.Shop;
using Management.Presentation.ViewModels.Scheduler;
using Management.Presentation.ViewModels.PointOfSale;
using Management.Presentation.Services.Navigation;
using Management.Presentation.Views.GymHome;
using Management.Presentation.Extensions;
using Management.Presentation.Views;
using Management.Presentation.ViewModels.Onboarding;
using Management.Application.DTOs;
using Management.Presentation.ViewModels.AccessControl;
using Management.Presentation.Views.Restaurant;
using Management.Presentation.Views.Dashboard;
using Management.Presentation.Views.Shared;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;

namespace Management.Presentation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// Acts as the Composition Root for Dependency Injection.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;
        private bool _isHandlingException = false;
        
        // Phase 1: Crash Fix - Disposal tracking and shutdown management
        private bool _isServiceProviderDisposed = false;
        private readonly System.Threading.CancellationTokenSource _appShutdownCts = new System.Threading.CancellationTokenSource();
        private IHost? _host;

        public App()
        {
            // Initialize LiveCharts Global Configuration
            LiveChartsCore.LiveCharts.Configure(config => 
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
            );

            // PostgreSQL Timestamp Fix (Critical for EF Core + Npgsql legacy compatibility)
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        private static Mutex? _instanceMutex;

        private bool EnsureSingleInstance()
        {
            _instanceMutex = new Mutex(true, "LuxuryaManagementSystem_SingleInstance", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show(
                    "Luxurya is already running in your taskbar. Multiple instances are not allowed.",
                    "Luxurya - Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                System.Windows.Application.Current.Shutdown();
                return false;
            }
            return true;
        }

        public void KillRunningTitanProcesses()
        {
            var currentPid = Process.GetCurrentProcess().Id;
            var names = new[] { "Luxurya.Client", "Luxurya", "Management.Presentation", "GymOS" };
            foreach (var name in names)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (p.Id == currentPid) continue;

                        Serilog.Log.Information("Killing competing process: {ProcessName} ({Id})", p.ProcessName, p.Id);
                        p.CloseMainWindow();
                        if (!p.WaitForExit(3000))
                            p.Kill();
                    }
                    catch { }
                }
            }
        }

        public bool TryRegisterZKTecoSdk(bool silent = true)
        {
            try
            {
                var regsvr32Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "regsvr32.exe"); // System = C:\Windows\System32 = 64-bit on 64-bit Windows

                var zkDllPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "zkemkeeper.dll");

                if (!File.Exists(zkDllPath))
                {
                    Serilog.Log.Warning("[Hardware] zkemkeeper.dll not found at {Path}", zkDllPath);
                    return false;
                }

                // Check if already registered first to avoid unnecessary elevation prompts
                if (Type.GetTypeFromProgID("zkemkeeper.ZKEM.1") != null)
                {
                    Serilog.Log.Information("[Hardware] ZKTeco SDK already registered.");
                    return true;
                }

                Serilog.Log.Information("[Hardware] Attempting ZKTeco SDK registration (Silent={Silent}, Tool={Tool})...", silent, regsvr32Path);

                var result = Process.Start(new ProcessStartInfo
                {
                    FileName = regsvr32Path,
                    Arguments = $"{(silent ? "/s" : "")} \"{zkDllPath}\"",
                    UseShellExecute = true,
                    Verb = "runas", // Requires admin elevation
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                
                if (result != null)
                {
                    bool exited = result.WaitForExit(10000);
                    if (exited)
                    {
                        // Verify registration succeeded
                        var type = Type.GetTypeFromProgID("zkemkeeper.ZKEM.1");
                        if (type != null)
                        {
                            Serilog.Log.Information("[Hardware] ZKTeco SDK registered successfully via regsvr32");
                            return true;
                        }
                    }
                }
                
                Serilog.Log.Error("[Hardware] ZKTeco SDK registration verification failed.");
                return false;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[Hardware] ZKTeco SDK registration failed — turnstile may not work");
                return false;
            }
        }



        private void LogTrace(string message)
        {
            try
            {
                File.AppendAllText("boot-trace.txt", $"{DateTime.Now:HH:mm:ss.fff} [TRACE] {message}{Environment.NewLine}");
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            LogTrace("--- APP STARTUP ---");



            if (!EnsureSingleInstance())
            {
                LogTrace("SingleInstance check failed. Shutting down.");
                return;
            }
            
            LogTrace("Initializing App...");
            InitializeApp();

            try
            {
                var icon = new BitmapImage(new Uri("pack://application:,,,/Assets/luxurya.ico"));
                foreach (Window w in System.Windows.Application.Current.Windows)
                {
                    w.Icon = icon;
                }
            }
            catch (Exception ex)
            {
                // Silently ignore or log if the taskbar icon fails to embed
            }

            base.OnStartup(e);
        }

        private void InitializeApp()
        {
            // --- EF CORE DESIGN TIME BYPASS ---
            // When running `dotnet ef migrations` or `database update`, the CLI attempts to 
            // build the host to discover DbContexts. If it tries to load WPF resources, it will crash.
            // We detect this by checking if the entry assembly is the 'ef' tool.
            var isEfCoreTool = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.FullName?.StartsWith("ef,") == true || a.FullName?.Contains("EntityFrameworkCore.Design") == true);
                
            if (isEfCoreTool)
            {
                // We are running under EF Core Tools. Set up a minimal Host just for service discovery
                // and completely skip any WPF UI/XAML initialization.
                Console.WriteLine("EF Core Design Time Detected. Bypassing WPF UI initialization.");
                _host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((context, builder) =>
                    {
                        builder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                               .AddJsonFile("appsettings.json", optional: true);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        Configuration = context.Configuration;
                        ConfigureServices(services);
                    })
                    .Build();
                return;
            }

            try 
            {
                LogTrace("Registering crash handlers...");
                // 1. Global Exception Handling (Register EARLY)
                this.DispatcherUnhandledException += OnDispatcherUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

                LogTrace("Initializing Serilog...");
                // 2. Setup Logging (Serilog)
                Serilog.Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                LogTrace("Serilog initialized. Building Host...");

                // 3. Setup Generic Host
                _host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((context, builder) =>
                    {
                        builder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                               .AddEnvironmentVariables();
                    })
                    .ConfigureServices((context, services) =>
                    {
                        Configuration = context.Configuration;
                        ConfigureServices(services);
                    })
                    .UseSerilog()
                    .Build();

                ServiceProvider = _host.Services;

                // FIX 7: Register Persistent ViewModels in NavigationStore
                var navStore = ServiceProvider.GetRequiredService<NavigationStore>();
                navStore.RegisterPersistentType(typeof(MembersViewModel));
                navStore.RegisterPersistentType(typeof(ShopViewModel));
                navStore.RegisterPersistentType(typeof(SettingsViewModel));
                navStore.RegisterPersistentType(typeof(DashboardViewModel));
                navStore.RegisterPersistentType(typeof(HistoryViewModel));
                navStore.RegisterPersistentType(typeof(FinanceAndStaffViewModel));
                navStore.RegisterPersistentType(typeof(RegistrationsViewModel));
                navStore.RegisterPersistentType(typeof(GymHomeViewModel));
                navStore.RegisterPersistentType(typeof(SalonHomeViewModel));
                navStore.RegisterPersistentType(typeof(RestaurantHomeViewModel));
                navStore.RegisterPersistentType(typeof(AppointmentsViewModel));

                // 4. Start the Host (Off-load to background thread to ensure STA initialization doesn't touch UI thread)
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        await _host.StartAsync(_appShutdownCts.Token);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Host failed to start");
                    }
                });

                // 5. Async Initialization
                _ = Task.Run(async () => await RunInitializationSequenceAsync(_appShutdownCts.Token))
                    .ContinueWith(t => 
                    {
                        if (t.IsFaulted) Serilog.Log.Fatal(t.Exception, "Main initialization sequence failed");
                    }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                HandleFatalStartupError(ex);
            }
        }

        private async Task RunInitializationSequenceAsync(CancellationToken ct = default)
        {
            try
            {

                // Phase 1: Check for cancellation before starting
                ct.ThrowIfCancellationRequested();

                Serilog.Log.Information("Starting initialization sequence...");
                
                // FIX 12: Check Clock Drift before anything else (NON-BLOCKING)
                UpdateStartupStatus("Checking system clock...");
                _ = CheckClockDriftAsync();

                // 1. Initialize Contexts (Must happen before Sync)
                Serilog.Log.Information("[INIT] Initializing Facility Context...");
                UpdateStartupStatus("Determining last facility...");
                var facilityContext = ServiceProvider.GetRequiredService<IFacilityContextService>();
                await Task.Run(() => facilityContext.Initialize());
                Serilog.Log.Information("[INIT] Facility Context Ready.");

                // 2. Initialize Localization (Load saved preference) EARLY
                // This ensures UI strings are loaded before the window is Shown.
                var localizationService = ServiceProvider.GetRequiredService<ILocalizationService>();
                var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
                string languageToLoad = facilityContext.LanguageCode;

                if (string.IsNullOrEmpty(languageToLoad))
                {
                    languageToLoad = "en"; // Fallback
                }
                
                localizationService.SetLanguage(languageToLoad);

                // 3. SHOW WINDOW
                Serilog.Log.Information("[INIT] Sending StartWindow command to Dispatcher...");
                UpdateStartupStatus("Launching UI...");
                await Current.Dispatcher.InvokeAsync(async () => {
                    try 
                    {
                        Serilog.Log.Information("[UI] Resolving navigation service...");
                        var navService = ServiceProvider.GetRequiredService<INavigationService>();

                        Serilog.Log.Information("[UI] Resolving AuthWindow and ViewModel...");
                        var authVm = ServiceProvider.GetRequiredService<AuthViewModel>();
                        var authWindow = ServiceProvider.GetRequiredService<AuthWindow>();

                        Serilog.Log.Information("[UI] Configuring MainWindow context...");
                        authWindow.DataContext = authVm;
                        Current.MainWindow = authWindow;
                        
                        Serilog.Log.Information("[UI] Executing Window.Show()...");
                        authWindow.Show();
                        Serilog.Log.Information("[UI] Window shown successfully. Initializing onboarding...");
                        
                        var tracker = ServiceProvider.GetRequiredService<IAppInitializationTracker>();
                        tracker.UpdateStatus("Starting application services...", 0.1);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "CRITICAL UI ERROR: Failed to show initial AuthWindow");
                        HandleFatalStartupError(ex);
                    }
                });

                // 4. Background Database Initialization
                Serilog.Log.Information("[App] Initializing database schema in background...");
                var trackerService = ServiceProvider.GetRequiredService<IAppInitializationTracker>();
                trackerService.UpdateStatus("Initializing database...", 0.3);
                
                var dbInitTask = InitializeDatabaseAsync(ct);
                await dbInitTask;

                // FIX 6: Pre-warm Supabase session from disk before SyncWorker's first cycle.
                // The SDK starts cold on every launch. SyncWorker fires its first PerformSyncAsync
                // within seconds. If the session is not warmed, any sync in that early window must
                // call TryRestoreSupabaseSessionAsync on-demand, which adds latency and could race.
                // This call is silent: it does NOT set SessionManager.CurrentUser (UI login still required).
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var sessionStorage = ServiceProvider.GetRequiredService<Management.Domain.Services.ISessionStorageService>();
                        var supabase = ServiceProvider.GetRequiredService<Supabase.Client>();
                        var storedSession = await sessionStorage.LoadSessionAsync();

                        if (storedSession != null &&
                            !storedSession.IsExpired &&
                            !storedSession.IsOfflineSession &&
                            storedSession.AccessToken != "OFFLINE_ACCESS_TOKEN")
                        {
                            var result = await supabase.Auth.SetSession(storedSession.AccessToken, storedSession.RefreshToken);
                            if (result?.User != null)
                                Serilog.Log.Information("[App] Startup: Supabase session pre-warmed for {Email}.", storedSession.Email);
                            else
                                Serilog.Log.Debug("[App] Startup: SetSession returned null \u2014 session will be restored on first sync attempt.");
                        }
                        else
                        {
                            Serilog.Log.Debug("[App] Startup: No valid cloud session on disk to pre-warm.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Non-fatal: startup continues normally
                        Serilog.Log.Warning(ex, "[App] Startup: Session pre-warm failed \u2014 will restore on first sync attempt.");
                    }
                }, ct);


                // --- Phase 6 HEALING: Full Auto-Discovery ---
                // Always run discovery to build the complete FacilityType → Guid map.
                // This must complete before CommitFacility() fires FacilityChanged so ViewModels
                // always receive a real GUID on their first query.
                Serilog.Log.Information("[App] Running full facility auto-discovery from SQLite...");
                try
                {
                    var dbContext = ServiceProvider.GetRequiredService<AppDbContext>();
                    var allFacilities = await dbContext.Facilities
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .Where(f => !f.IsDeleted)
                        .ToListAsync(ct);

                    if (allFacilities.Count > 0)
                    {
                        var map = allFacilities
                            .GroupBy(f => f.Type)
                            .ToDictionary(g => g.Key, g => g.First().Id);
                        facilityContext.UpdateFacilities(map);
                        Serilog.Log.Information("[App] Auto-discovery populated {Count} facility mappings: {Types}",
                            map.Count, string.Join(", ", map.Select(kv => $"{kv.Key}={kv.Value}")));
                    }
                    else
                    {
                        Serilog.Log.Warning("[App] Auto-discovery: no facilities found in local DB. CommitFacility will fire with empty context.");
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "[App] Auto-discovery failed. CommitFacility will proceed with whatever is in memory.");
                }

                // Commit: fire FacilityChanged NOW with the fully-populated map.
                // The guard in SwitchFacility will block the event if the GUID is still empty.
                facilityContext.CommitFacility();

                // FIX 4 (CORRECTED PLACEMENT): Launch license check immediately after CommitFacility —
                // the earliest safe moment. The Supabase RPC now runs in parallel with all the
                // synchronous setup work below (view mappings, nav registry, diagnostics).
                UpdateStartupStatus("Verifying license...");
                Serilog.Log.Information("[App] Launching license check in background (parallel with setup)...");
                var licenseTask = Task.Run(() => RunStartupSecurityGuard(ServiceProvider), ct);

                // Synchronize SessionManager with the committed facility context
                var sessionManager = ServiceProvider.GetRequiredService<Management.Presentation.Services.State.SessionManager>();
                sessionManager.CurrentFacility = facilityContext.CurrentFacility;

                // ROLE-AWARE REFINEMENT: Startup cleanup removed.
                // Cleanup now happens during the login flow specifically for regular staff,
                // while bypassing for Owners to preserve their management data.
                var currentFacilityGuid = facilityContext.CurrentFacilityId;
                
                // ORPHANED STAFF HEALING (EF CORE DATA RESCUE)
                // We run this once during startup to properly resurrect staff members accidentally 
                // purged during the security rollback, ensuring EF tracking triggers the sync outbox.
                if (currentFacilityGuid != Guid.Empty)
                {
                    try
                    {
                        var staffRepo = ServiceProvider.GetRequiredService<IStaffRepository>();
                        await staffRepo.RescueOrphanedStaffMembersAsync(currentFacilityGuid);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "[App] Failed to rescue orphaned staff members during startup.");
                    }
                }

                // 2. Initial Sync Logic
                var syncService = ServiceProvider.GetRequiredService<Management.Application.Interfaces.App.ISyncService>();
            
                // Check for initial migration need moved to background task
                // to avoid blocking application startup.
            
                // _ = syncService.StartAsync(CancellationToken.None); // Removed: SyncWorker is IHostedService and starts with Host
                // 3.5. Register View Mappings
                var mappingService = ServiceProvider.GetRequiredService<IViewMappingService>();
                mappingService.Register<ConflictResolutionViewModel, ConflictResolutionView>();
                mappingService.Register<BookingViewModel, BookingModal>();
                mappingService.Register<SalonAddStaffViewModel, Management.Presentation.Views.Salon.AddStaffView>();
                mappingService.Register<CompletionViewModel, CompletionModal>();
                mappingService.Register<RfidAccessControlViewModel, AccessControlModal>();
                mappingService.Register<AppointmentDetailViewModel, AppointmentDetailModal>();
                mappingService.Register<PayrollViewModel, PayrollView>();
                mappingService.Register<PayrollHistoryViewModel, PayrollHistoryView>();
                mappingService.Register<RevenueHistoryViewModel, RevenueHistoryView>();
                mappingService.Register<OccupancyHistoryViewModel, OccupancyHistoryView>();
                mappingService.Register<AppExitViewModel, Management.Presentation.Views.Shell.AppExitView>();
                mappingService.Register<ConfirmationModalViewModel, Management.Presentation.Views.Shared.ConfirmationModalWindow>();
                mappingService.Register<InventoryHistoryViewModel, InventoryHistoryView>();
                mappingService.Register<LogoutConfirmationViewModel, LogoutConfirmationWindow>();
                // RegisterWalkInModal is now a UserControl handled via DataTemplates in App.xaml
                // and displayed in the MainWindow overlay via ModalNavigationStore.
                // RestaurantOrderingViewModel is a UserControl navigated to via NavigationService, 
                // so it doesn't need to be registered in the Modal ViewMappingService.

                // 4. Initialize Navigation Registry
                var registry = ServiceProvider.GetRequiredService<INavigationRegistry>();
                PopulateNavigationRegistry(registry);
                
                // Register Home Views (Decoupling MainViewModel)
                registry.RegisterHomeView<GymHomeViewModel>(Management.Domain.Enums.FacilityType.Gym);
                registry.RegisterHomeView<SalonHomeViewModel>(Management.Domain.Enums.FacilityType.Salon);
                registry.RegisterHomeView<RestaurantHomeViewModel>(Management.Domain.Enums.FacilityType.Restaurant);
                registry.RegisterHomeView<DashboardViewModel>(Management.Domain.Enums.FacilityType.General); // Default fallback


                // 4.5. Initialize Diagnostic System (Parallelized)
                ct.ThrowIfCancellationRequested();
                Serilog.Log.Information("Starting Diagnostic and Connectivity checks in background...");
                var diagnosticService = ServiceProvider.GetRequiredService<Management.Application.Services.IDiagnosticService>();
                
                var diagTask = Task.Run(async () => 
                {
                    await diagnosticService.StartBindingErrorListenerAsync();
                    await diagnosticService.ValidateDependencyInjectionAsync(ServiceProvider);
                    await diagnosticService.TestSupabaseConnectivityAsync();
                }, ct).ContinueWith(t => 
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        Serilog.Log.Error(t.Exception.Flatten(), "Diagnostic background task failed.");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);

                // Await license result — it has been running in parallel since CommitFacility.
                Serilog.Log.Information("[App] Awaiting license check result...");
                ct.ThrowIfCancellationRequested();
                bool isLicensed = await licenseTask;
                Serilog.Log.Information("[App] License check resolved: {Result}", isLicensed);

                // Navigation Routing
                await Current.Dispatcher.InvokeAsync(async () => 
                {
                    try 
                    {
                        var navService = ServiceProvider.GetRequiredService<INavigationService>();

                        if (!isLicensed)
                        {
                            Serilog.Log.Information("[App] Device not licensed. Navigating to Activation...");
                            await navService.NavigateToAsync<LicenseEntryViewModel>();
                        }
                        else
                        {
                            var stateStore = ServiceProvider.GetRequiredService<IOnboardingStateStore>();
                            var authService = ServiceProvider.GetRequiredService<IAuthenticationService>();
                            var tenantService = ServiceProvider.GetRequiredService<ITenantService>();
                            
                            // TIER 0: Expansion Flow Bypass
                            // If we have flagged this as an Expansion Flow (machine verified via license),
                            // we skip the Cloud Owner verification entirely and move to Facility Selection.
                            if (stateStore.IsExpansionFlow || (stateStore.TargetTenantId.HasValue && stateStore.TargetTenantId != Guid.Empty))
                            {
                                Serilog.Log.Information("[App] Expansion Flow confirmed: Tenant {Id}. Bypassing owner check and routing to Splash Onboarding.", stateStore.TargetTenantId);
                                await navService.NavigateToSplashAsync();
                                return;
                            }

                            Guid activeTenantId = tenantService.GetTenantId() ?? Guid.Empty;
                            bool hasOwner = false;
                            
                            if (activeTenantId != Guid.Empty)
                            {
                                Serilog.Log.Information("[App] Checking verification for Tenant {Id}...", activeTenantId);
                                hasOwner = await authService.TenantHasOwnerAccountAsync(activeTenantId);
                            }
                            
                            // TIER 2: If Cloud/Tenant check failed OR was missing (Offline case), check local existence
                            if (!hasOwner)
                            {
                                Serilog.Log.Information("[App] No cloud owner verified or Tenant missing. Performing local data probe...");
                                // Passing Guid.Empty forces the authentication service to check for ANY local staff (Offline Safety Net)
                                hasOwner = await authService.TenantHasOwnerAccountAsync(Guid.Empty);
                            }

                            if (hasOwner)
                            {
                                Serilog.Log.Information("[App] Owner/Staff confirmed. Navigating to Splash Onboarding...");
                                await navService.NavigateToSplashAsync();
                            }
                            else
                            {
                                Serilog.Log.Information("[App] No owner found in cloud or local. Navigating to Account Setup...");
                                await navService.NavigateToAsync<OnboardingOwnerViewModel>();
                            }
                        }


                        Serilog.Log.Information("[App] Navigation routing complete.");

                        // 8. Background Hardware Check (Offloaded from UI thread)
                        _ = Task.Run(() => 
                        {
                            try 
                            {
                                var turnstileService = ServiceProvider.GetRequiredService<IHardwareTurnstileService>();
                                if (!turnstileService.IsSdkAvailable)
                                {
                                    Serilog.Log.Warning("ZKTeco SDK not found in background check. Gate control will be disabled.");
                                    
                                    Current.Dispatcher.InvokeAsync(() => 
                                    {
                                        var toastService = ServiceProvider.GetRequiredService<Management.Application.Interfaces.App.IToastService>();
                                        toastService.ShowError("ZKTeco SDK not registered. Gate hardware is disabled.", "Hardware Error");
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Error(ex, "Error during background hardware check");
                            }
                        });
                        // 9. Fire Background Update Check
                        _ = Task.Run(async () => 
                        {
                            try
                            {
                                // Wait 15 seconds so startup isn't bottlenecked and user has logged in
                                await Task.Delay(15000);
                                var updateService = ServiceProvider.GetRequiredService<Management.Presentation.Services.IUpdateService>();
                                await updateService.CheckForUpdatesAsync();
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Error(ex, "Background update check failed");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Error during UI-thread startup routing");
                    }
                });
                Serilog.Log.Information("Startup Sequence Complete.");
                
                // Finalize Initialization Tracker
                var tracker = ServiceProvider.GetRequiredService<IAppInitializationTracker>();
                tracker.SetComplete();
            }
            catch (OperationCanceledException)
            {
                Serilog.Log.Information("Initialization sequence cancelled (application shutting down).");
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "Fatal error during initialization sequence.");
                Dispatcher.InvokeAsync(() =>
                {
                    HandleFatalStartupError(ex);
                });
            }
        }

        public async Task LaunchMainWindowAsync()
        {
            await Current.Dispatcher.InvokeAsync(async () =>
            {
                try 
                {
                    Serilog.Log.Information("Handoff: Launching Main Shell...");
                                  // CRITICAL: Reset all stateful Singletons (State Isolation) before re-establishing UI
                    ResetApplicationState();
                    
                    // CRITICAL: Blank the navigation store immediately so no window renders old/ghost content
                    var navStore = ServiceProvider.GetRequiredService<NavigationStore>();
                    navStore.CurrentViewModel = null;

                    // CRITICAL: If the current shell is AuthWindow, tell its ViewModel to stop listening to NavigationStore
                    if (Current.MainWindow?.DataContext is AuthViewModel authVm)
                    {
                        authVm.PrepareForHandoff();
                    }

                    var mainWindow = ServiceProvider.GetRequiredService<Management.Presentation.Views.Shell.MainWindow>();
                    var oldWindow = Current.MainWindow;

                    // CRITICAL: Orchestrate Startup Theme BEFORE showing the window to prevent UI flicker.
                    // Read from the local theme-prefs.json file written by ThemeManager on every user change.
                    // This bypasses the DB entirely, avoiding the Guid.Empty race condition that previously
                    // caused GetAppearanceSettingsAsync to return default values and reset the user's preferences.
                    try
                    {
                        var facilityContextService = ServiceProvider.GetRequiredService<Management.Domain.Services.IFacilityContextService>();
                        var (savedTheme, savedPalette) = Management.Presentation.Services.ThemeManager.LoadPrefs();

                        var palette = savedPalette ?? Management.Presentation.Services.LightPalette.Default;
                        var theme   = savedTheme   ?? Management.Presentation.Services.AppTheme.Light;

                        // Apply palette first, then theme (SetTheme also calls SetFacility which re-applies palette)
                        Management.Presentation.Services.ThemeManager.SetLightPalette(palette);
                        Management.Presentation.Services.ThemeManager.SetTheme(theme, facilityContextService.CurrentFacility);

                        // Surgical Fix: Force UI sync after startup theme application to resolve desynchronized toggle state
                        WeakReferenceMessenger.Default.Send(new AppearanceChangedMessage(new AppearanceChangeInfo(
                            theme == Management.Presentation.Services.AppTheme.Light, palette.ToString(), IsThemeChange: true)));

                        Serilog.Log.Information("[INIT] Startup theme applied from local prefs: Theme={Theme}, Palette={Palette}", theme, palette);
                    }
                    catch (Exception themeEx)
                    {
                        Serilog.Log.Error(themeEx, "[INIT] Failed to apply startup theme from local prefs. Falling back to defaults.");
                        var facilityContextService = ServiceProvider.GetRequiredService<Management.Domain.Services.IFacilityContextService>();
                        Management.Presentation.Services.ThemeManager.SetLightPalette(Management.Presentation.Services.LightPalette.Default);
                        Management.Presentation.Services.ThemeManager.SetTheme(Management.Presentation.Services.AppTheme.Light, facilityContextService.CurrentFacility);
                    }

                    Current.MainWindow = mainWindow;
                    mainWindow.Show();
                    Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

                    if (oldWindow != null)
                    {
                        oldWindow.Hide(); // Hide immediately to prevent overlap ghosting
                        oldWindow.Close();
                    }

                    // Navigation is handled by MainViewModel.ResetState() → InitializeInitialView()
                    // which is called by the IStateResettable loop above. No explicit call needed here.
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "Failed to handoff to MainWindow");
                    HandleFatalStartupError(ex);
                }
            });
        }

        // FIX 3: Keep backward-compatible sync entry point that callers not yet converted can use
        public void LaunchMainWindow() => _ = LaunchMainWindowAsync();

        public async Task LogoutAsync()
        {
            await Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    Serilog.Log.Information("Handoff: Logging out, switching to Auth Shell...");
                    
                    // CRITICAL: Clear the navigation state FIRST so no NEW windows see old content
                    var navStore = ServiceProvider.GetRequiredService<NavigationStore>();
                    navStore.CurrentViewModel = null;

                    // CRITICAL: Clear all in-memory singleton state
                    ResetApplicationState();

                    var authVm = ServiceProvider.GetRequiredService<AuthViewModel>();
                    var authWindow = ServiceProvider.GetRequiredService<AuthWindow>();
                    var navService = ServiceProvider.GetRequiredService<INavigationService>();
                    var oldWindow = Current.MainWindow;

                    authWindow.DataContext = authVm;
                    Current.MainWindow = authWindow;
                    authWindow.Show();
                    
                    if (oldWindow is Management.Presentation.Views.Shell.MainWindow mainWin)
                    {
                        mainWin.PrepareForHandoff();
                    }

                    oldWindow?.Close();

                    // Navigate to Splash Onboarding view within Auth shell (5 slides)
                    await navService.NavigateToSplashAsync();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "Failed to switch to Auth Window");
                    HandleFatalStartupError(ex);
                }
            });
        }

        // FIX 3: Keep backward-compatible sync entry point
        public void Logout() => _ = LogoutAsync();

        private void ResetApplicationState()
        {
            try
            {
                Serilog.Log.Information("State Isolation: Resetting all resettable stores...");
                var resettables = ServiceProvider.GetServices<Management.Domain.Interfaces.IStateResettable>();
                foreach (var resettable in resettables)
                {
                    resettable.ResetState();
                }

                // Re-synchronize SessionManager after reset (ensures it reflects the currently committed facility context)
                var facilityContext = ServiceProvider.GetService<Management.Domain.Services.IFacilityContextService>();
                var sessionManager = ServiceProvider.GetService<Management.Presentation.Services.State.SessionManager>();
                if (sessionManager != null && facilityContext != null)
                {
                    sessionManager.CurrentFacility = facilityContext.CurrentFacility;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to reset application state");
            }
        }

        /// <summary>
        /// Re-initializes services that depend on a valid tenant context/license.
        /// This is called after onboarding to ensure the app is operational.
        /// </summary>
        public async Task ReinitializeOperationalServicesAsync()
        {
            Serilog.Log.Information("[App] Re-initializing operational services after onboarding...");
            
            // 1. Re-run Migration for the new tenant
            await InitializeDatabaseAsync();
            
            // 2. Re-init Resilience (loads pending actions, etc)
            await InitializeResilienceAsync(ServiceProvider);
            
            Serilog.Log.Information("[App] Operational services re-initialized.");
        }

        /// <summary>
        /// SECURITY FIX 1D: Removes staff records from local SQLite whose FacilityId does not match
        /// the current PC's configured facility. These records were incorrectly synced by the old
        /// unfiltered PullStaffMembersAsync query (pre-security fix). Runs once at startup after
        /// facility context is committed.
        /// </summary>
        private async Task InitializeResilienceAsync(IServiceProvider services)
        {
            var resilienceService = services.GetRequiredService<IResilienceService>();
            if (resilienceService is ResilienceService rs)
            {
                try 
                {
                    await rs.InitializeAsync();
                }
                catch (Exception resEx)
                {
                    Serilog.Log.Error(resEx, "Failed to initialize ResilienceService");
                    var diagnosticService = services.GetRequiredService<Management.Application.Services.IDiagnosticService>();
                    diagnosticService.LogError(Management.Application.Services.DiagnosticCategory.Runtime, "Resilience Init", resEx.Message, resEx, Management.Application.Services.DiagnosticSeverity.Error);
                }
            }
        }

        private void HandleFatalStartupError(Exception ex)
        {
            Serilog.Log.Fatal(ex, "FATAL ERROR during startup");
            var errorDetails = $"FATAL STARTUP ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}";
            Console.WriteLine(errorDetails);
            File.WriteAllText("boot-fatal-debug.txt", errorDetails);
            
            if (_isHandlingException) return;
            _isHandlingException = true;

            Current.Dispatcher.InvokeAsync(() => {
                try 
                {
                    // Ensure the error is logged to the diagnostic service so it appears in the window
                    var diagnosticService = ServiceProvider?.GetService<Management.Application.Services.IDiagnosticService>();
                    diagnosticService?.LogError(
                        Management.Application.Services.DiagnosticCategory.Startup,
                        "Startup",
                        ex.Message,
                        ex,
                        Management.Application.Services.DiagnosticSeverity.Fatal
                    );

                    var diagnosticViewModel = ServiceProvider?.GetService<DiagnosticViewModel>();
                    if (diagnosticViewModel != null)
                    {
                        var diagnosticWindow = new Views.Diagnostic.DiagnosticView(diagnosticViewModel);
                        diagnosticWindow.Show();
                        Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
                    }
                    else 
                    {
                        System.IO.File.WriteAllText("startup_crash.txt", ex.ToString()); MessageBox.Show($"Fatal Error during startup:\n\n{ex.Message}", "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch 
                {
                    System.IO.File.WriteAllText("startup_crash.txt", ex.ToString()); MessageBox.Show($"Fatal Error during startup:\n\n{ex.Message}", "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Shutdown might be too aggressive if we want the diagnostic window to stay open
                // For a fatal startup error, we usually HAVE to shutdown eventually, but let's let the user see the console.
                // Shutdown(); 
            });
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // --- CONFIGURATION ---
            services.AddSingleton<IConfiguration>(Configuration);

            // --- LOGGING ---
            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

            // --- CACHING ---
            services.AddMemoryCache();

            // --- MEDIATR ---
            services.AddMediatR(cfg => {
                cfg.RegisterServicesFromAssembly(typeof(AccountStore).Assembly);
                cfg.RegisterServicesFromAssembly(typeof(AppDbContext).Assembly);
                cfg.RegisterServicesFromAssembly(typeof(App).Assembly);
            });

            // Explicitly register Home ViewModels as notification handlers to ensure the singleton instance is used
            // REFACTORED: Use Bridge Pattern to decouple ViewModels from MediatR
            // services.AddSingleton<INotificationHandler<FacilityActionCompletedNotification>>(s => s.GetRequiredService<GymHomeViewModel>());
            // services.AddSingleton<INotificationHandler<FacilityActionCompletedNotification>>(s => s.GetRequiredService<SalonHomeViewModel>());
            // services.AddSingleton<INotificationHandler<FacilityActionCompletedNotification>>(s => s.GetRequiredService<RestaurantHomeViewModel>());
            
            // The Bridge is automatically registered via MediatR's assembly scanning on App.Assembly.
            // Do NOT register it again here as it causes double-handled notifications (leading to duplicate UI items and DbContext concurrency exceptions).

            // --- TENANT CONTEXT ---
            services.AddSingleton<ITenantService, Infrastructure.Services.TenantService>();

            // --- INFRASTRUCTURE: DATABASE ---
            // CRITICAL: Registered as Transient for WPF to avoid Captive Dependency in Singletons (MainViewModel)
            // and because there is no per-request scope in desktop apps.
            // Repositories will get fresh contexts, but Singletons (like Stores) should be careful.
            var connectionString = Configuration.GetConnectionString("SupabaseConnection");
            var dbMode = Configuration["Database:Mode"] ?? "LocalFirst";
            bool isDevBypass = dbMode == "LocalFirst"; 
            
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                var databaseMode = Configuration["Database:Mode"] ?? "LocalFirst";

                if (databaseMode == "LocalFirst")
                {
                    var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Luxurya");
                    if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
                    
                    var dbPath = Path.Combine(dbFolder, "GymManagement.db");
                    options.UseSqlite($"Data Source={dbPath};Mode=ReadWriteCreate;Foreign Keys=True;Pooling=True;", b => b.MigrationsAssembly("Management.Infrastructure"));
                }
                else
                {
                    // Supabase Free Tier Fix: Maximum Pool Size=10; to stay within the 15-20 connection limit
                    var supabaseConnStr = connectionString;
                    if (!string.IsNullOrEmpty(supabaseConnStr) && !supabaseConnStr.Contains("Maximum Pool Size"))
                    {
                        supabaseConnStr += "Maximum Pool Size=10;";
                    }
                    options.UseNpgsql(supabaseConnStr ?? string.Empty, b => b.MigrationsAssembly("Management.Infrastructure"));
                }
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();

                // Interceptors are now handled via constructor injection and OnConfiguring to avoid resolution loops

            }, ServiceLifetime.Scoped, ServiceLifetime.Singleton);

            // --- INFRASTRUCTURE: EXTERNAL ---
            // Supabase Client (Singleton)
            // Supabase Client (Singleton)
            services.AddSingleton(provider =>
            {
                var storage = provider.GetRequiredService<Management.Application.Services.ISecureStorageService>();
                
                // Fix 6: Secure Supabase Credentials - REFACTORED: Use synchronous Get to avoid UI thread blocking
                var url = storage.Get("SupabaseUrl");
                var key = storage.Get("SupabaseKey");
                
                // Fallback to configuration if not in secure storage (for initial setup)
                if (string.IsNullOrEmpty(url))
                {
                    url = Configuration["Supabase:Url"];
                    key = Configuration["Supabase:Key"];
                }

                if (string.IsNullOrEmpty(url))
                {
                    url = "https://setup-required.local";
                    key = "setup-required";
                }

                var options = new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true
                };
                
                return new Supabase.Client(url, key, options);
            });

            // Hardware Drivers
            services.AddSingleton<Management.Application.Interfaces.IHardwareService, HardwareService>();
            services.AddTransient<IOnboardingService, OnboardingService>();
            services.AddTransient<ILicenseService, LicenseService>();
            
            // Peripherals
            services.AddSingleton<ScannerService>();
            services.AddSingleton<IPrinterService, EscPosPrinterService>();
            
            // ZKTeco Integration
            var turnstileSection = Configuration.GetSection("Turnstile");
            var turnstileConfig = turnstileSection.Get<TurnstileConfig>() ?? new TurnstileConfig();
            services.AddSingleton(turnstileConfig);
            
            if (turnstileConfig.UseMock)
            {
                services.AddSingleton<IHardwareTurnstileService, Management.Infrastructure.Hardware.MockTurnstileService>();
            }
            else
            {
                services.AddSingleton<IHardwareTurnstileService, ZKTecoTurnstileService>();
            }
            
            // Keep legacy interfaces for backward compatibility if needed, 
            // but mapped to the new unified service where possible.
            services.AddSingleton<IRfidReader, RfidReaderDevice>(); 
            services.AddTransient<Management.Application.Services.IMenuService, Management.Infrastructure.Services.MenuService>();

            // --- DATABASE INTERCEPTORS ---
            services.AddTransient<Management.Infrastructure.Data.Interceptors.ShadowPropertyInterceptor>();
            services.AddTransient<Management.Infrastructure.Data.Interceptors.OutboxInterceptor>();
            services.AddTransient<Management.Infrastructure.Data.AuditableEntityInterceptor>();
            services.AddTransient<Management.Application.Interfaces.IOrderService, Management.Application.Services.OrderService>();
            services.AddTransient<Management.Application.Services.IInventoryService, Management.Infrastructure.Services.InventoryService>();
            services.AddTransient<Management.Presentation.ViewModels.Restaurant.InventoryViewModel>();
            services.AddTransient<Management.Presentation.ViewModels.Restaurant.OpenOrdersViewModel>();

            // --- UNIT OF WORK ---
            services.AddScoped<IUnitOfWork, Management.Infrastructure.Data.UnitOfWork>();

            // --- UPDATE SERVICE ---
            services.AddSingleton<Management.Presentation.Services.IUpdateService, Management.Presentation.Services.UpdateService>();

            // --- REPOSITORIES (Data Access - Scoped) ---
            services.AddScoped<IStaffRepository, StaffRepository>();
            services.AddScoped<IRepository<StaffMember>>(s => s.GetRequiredService<IStaffRepository>());
            
            services.AddScoped<IMenuRepository, MenuRepository>();
            services.AddScoped<IRepository<RestaurantMenuItem>>(s => s.GetRequiredService<IMenuRepository>());
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IRestaurantOrderRepository, OrderRepository>();
            services.AddScoped<IRepository<RestaurantOrder>>(s => s.GetRequiredService<IOrderRepository>());

            services.AddScoped<IMemberRepository, MemberRepository>();
            services.AddScoped<IRepository<Member>>(s => s.GetRequiredService<IMemberRepository>());

            services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            services.AddScoped<IRepository<Management.Domain.Models.Salon.Appointment>>(s => s.GetRequiredService<IAppointmentRepository>());
            
            services.AddScoped<IRegistrationRepository, RegistrationRepository>();
            services.AddScoped<IAccessEventRepository, AccessEventRepository>();
            
            services.AddScoped<ISaleRepository, SaleRepository>();
            services.AddScoped<IRepository<Sale>>(s => s.GetRequiredService<ISaleRepository>());
            
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IRepository<Product>>(s => s.GetRequiredService<IProductRepository>());
            services.AddScoped<IInventoryRepository, InventoryRepository>();
            
            services.AddScoped<ITurnstileRepository, TurnstileRepository>();
            
            services.AddScoped<IReservationRepository, ReservationRepository>();
            services.AddScoped<IRepository<Reservation>>(s => s.GetRequiredService<IReservationRepository>());
            
            services.AddScoped<IPayrollRepository, PayrollRepository>();
            services.AddScoped<MembershipPlanRepository>();
            services.AddScoped<IMembershipPlanRepository>(s => 
                new CachedMembershipPlanRepository(s.GetRequiredService<MembershipPlanRepository>(), s.GetRequiredService<IMemoryCache>()));
            services.AddScoped<IRepository<MembershipPlan>>(s => s.GetRequiredService<IMembershipPlanRepository>());
            services.AddScoped<IIntegrationRepository, IntegrationRepository>();
            services.AddScoped<IGymSettingsRepository, GymSettingsRepository>();
            services.AddScoped<ISalonSettingsRepository, SalonSettingsRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IFacilityScheduleRepository, FacilityScheduleRepository>();
            services.AddScoped<IRepository<FacilitySchedule>>(s => s.GetRequiredService<IFacilityScheduleRepository>());
            services.AddScoped<ITableRepository, TableRepository>();
            services.AddScoped<IRepository<Management.Domain.Models.Restaurant.TableModel>>(s => s.GetRequiredService<ITableRepository>());
            services.AddScoped<IFacilityZoneRepository, FacilityZoneRepository>();
            services.AddScoped<ISalonServiceRepository, SalonServiceRepository>();
            services.AddScoped<IRepository<Management.Domain.Models.Salon.SalonService>>(s => s.GetRequiredService<ISalonServiceRepository>());

            // --- STORES (State Management - SINGLETONS) ---
            services.AddSingleton<NavigationStore>();
            services.AddSingleton<ModalNavigationStore>();
            services.AddSingleton<AccountStore>();
            services.AddSingleton<SaleStore>();
            services.AddSingleton<MemberStore>();
            services.AddSingleton<RegistrationStore>();
            services.AddSingleton<AccessEventStore>();
            services.AddSingleton<ProductStore>();
            services.AddSingleton<TurnstileStore>();
            services.AddSingleton<SyncStore>();
            services.AddSingleton<NotificationStore>();

            // Register all resettable stores for unified reset during facility switch
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<NavigationStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<ModalNavigationStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<AccountStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SaleStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<MemberStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<RegistrationStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<AccessEventStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<ProductStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<TurnstileStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SyncStore>());
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<NotificationStore>());
            services.AddSingleton<IStateResettable>(s => (IStateResettable)s.GetRequiredService<IAuthenticationService>());
            
            // Register Home ViewModels and Shell ViewModels as Resettable
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<MainViewModel>());

            // --- DOMAIN SERVICES (Business Logic) ---
            services.AddTransient<IMembershipService, MembershipService>();
            services.AddTransient<IMemberService, MemberService>();
            services.AddTransient<IStaffService, StaffService>();
            services.AddTransient<IRegistrationService, RegistrationService>();
            services.AddTransient<IWebsiteRegistrationService, WebsiteRegistrationService>();
            services.AddTransient<IAccessEventService, AccessEventService>();
            services.AddTransient<IMemberAccessService, MemberAccessService>();
            services.AddTransient<IProductService, ProductService>();
            services.AddTransient<ISaleService, SaleService>();
            services.AddTransient<IReservationService, ReservationService>();
            services.AddTransient<IMembershipPlanService, MembershipPlanService>();
            services.AddTransient<ISessionMonitorService, SessionMonitorService>();
            services.AddHttpClient<Management.Domain.Services.IEmailService, Management.Infrastructure.Services.BrevoEmailService>();
            // Added Missing Domain Services
            services.AddTransient<Management.Application.Interfaces.App.IGymOperationService, Management.Application.Services.GymOperationService>();
            services.AddSingleton<Management.Application.Interfaces.App.IAudioService, Management.Infrastructure.Services.Audio.AudioService>();
            services.AddSingleton<IAccessControlCache, AccessControlCache>();
            services.AddTransient<ITableService, TableService>();
            services.AddTransient<IAccessControlService, AccessControlService>();
            services.AddScoped<IPricingService, PricingService>();
            services.AddTransient<IPromotionService, PromotionService>();
            services.AddScoped<IDiscountService, DiscountService>();
            // The line below was moved up as part of the change.
            // services.AddSingleton<IAccessControlCache, AccessControlCache>();

            // --- APPLICATION SERVICES (Orchestration) ---
            services.AddSingleton<Management.Domain.Services.IConnectionService, ConnectionService>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddTransient<ITurnstileService, TurnstileService>();
            services.AddTransient<IFinanceService, FinanceService>();
            services.AddTransient<ISettingsService, SettingsService>();
            services.AddTransient<IBackupService, BackupService>();
            services.AddSingleton<Management.Domain.Services.ISessionStorageService, SessionStorageService>();
            services.AddSingleton<Management.Domain.Services.IFacilityContextService, Management.Presentation.Services.FacilityContextService>();
            services.AddSingleton<ITerminologyService, TerminologyService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddTransient<ICommandPaletteService, CommandPaletteService>();
            services.AddSingleton<Management.Presentation.Services.Restaurant.IReceiptPrintingService, Management.Presentation.Services.Restaurant.ReceiptPrintingService>();
            services.AddSingleton<ISalonService, SalonServiceImplementation>();
            services.AddTransient<IAppointmentService, AppointmentService>();
            services.AddTransient<ISalonDashboardService, SalonDashboardService>();
            services.AddSingleton<IResilienceService, ResilienceService>();
            services.AddSingleton<IUndoService, UndoService>();
            // --- DASHBOARD AGGREGATORS ---
            services.AddTransient<IDashboardAggregator, FinancialAggregator>();
            services.AddTransient<IDashboardAggregator, GymAggregator>();
            services.AddTransient<IDashboardAggregator, SalonAggregator>();
            services.AddTransient<IDashboardAggregator, RestaurantAggregator>();
            services.AddTransient<IDashboardAggregator, StaffAggregator>();
            services.AddTransient<IDashboardAggregator, TrendAggregator>();
            services.AddTransient<IDashboardAggregator, ActivityAggregator>();
            services.AddTransient<IDashboardAggregator, RetentionAggregator>();
            services.AddTransient<IDashboardAggregator, GrowthAggregator>();
            services.AddTransient<IDashboardAggregator, BehavioralAggregator>();
            services.AddTransient<IDashboardAggregator, ClassPerformanceAggregator>();
            services.AddTransient<IDashboardAggregator, SalonPerformanceAggregator>();

            services.AddTransient<IDashboardService, DashboardService>();
            services.AddTransient<ITransactionService, TransactionService>();
            services.AddSingleton<ISecurityService, SecurityService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddTransient<IReportingService, ReportingService>();
            services.AddSingleton<Management.Application.Services.ISecureStorageService, Management.Presentation.Services.Infrastructure.SecureStorageService>();
            services.AddTransient<IProductInventoryService, ProductInventoryService>();

            // --- DIAGNOSTIC SYSTEM ---
            services.AddSingleton<Management.Application.Services.IDiagnosticService, Management.Presentation.Services.DiagnosticService>();
            services.AddSingleton<DiagnosticViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<DiagnosticViewModel>());
            services.AddSingleton<ConnectivityViewModel>(); // Shared VM for banner

            // Sync Engine
            services.AddSingleton<Management.Application.Interfaces.App.ISyncService, SyncService>();
            services.AddSingleton<Management.Application.Interfaces.App.ISyncEventDispatcher, Management.Infrastructure.Services.SyncEventDispatcher>();
            services.AddHostedService<SyncWorker>();
            
            services.AddSingleton<SupabaseRealtimeService>();
            services.AddHostedService(provider => provider.GetRequiredService<SupabaseRealtimeService>());
            
            services.AddHostedService<AccessMonitoringWorker>();
            services.AddHostedService<SnapshotSyncWorker>();

            // History Providers
            services.AddTransient<Management.Application.Interfaces.App.IHistoryProvider, Management.Application.Services.History.GymHistoryProvider>();
            services.AddTransient<Management.Application.Interfaces.App.IHistoryProvider, Management.Application.Services.History.SalonHistoryProvider>();
            services.AddTransient<Management.Application.Interfaces.App.IHistoryProvider, Management.Application.Services.History.RestaurantHistoryProvider>();


            // --- PRESENTATION SERVICES (UI) ---
            services.AddSingleton<CommunityToolkit.Mvvm.Messaging.IMessenger>(CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default);
            var wpfDispatcher = new WpfDispatcher(System.Windows.Application.Current.Dispatcher);
            services.AddSingleton<IDispatcher>(wpfDispatcher);
            services.AddSingleton<IDispatcherService>(wpfDispatcher);
            
            // Session and User Management
            services.AddSingleton<SessionManager>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SessionManager>());
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<Management.Application.Interfaces.App.IToastService>(s => s.GetRequiredService<INotificationService>() as NotificationService ?? throw new InvalidOperationException("NotificationService not registered"));
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<Management.Application.Interfaces.App.IAppInitializationTracker, Management.Presentation.Services.Application.AppInitializationTracker>();
            
            services.AddTransient<ISearchService, SearchService>();
            services.AddSingleton<IBreadcrumbService, Management.Presentation.Services.Application.BreadcrumbService>();

            services.AddSingleton<INavigationService, NavigationService>(provider =>
                new NavigationService(
                    provider.GetRequiredService<NavigationStore>(),
                    viewModelType => (ViewModelBase)provider.GetRequiredService(viewModelType),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Management.Application.Interfaces.App.IToastService>(),
                    provider.GetRequiredService<INavigationRegistry>(),
                    provider.GetRequiredService<SessionManager>(),
                    provider.GetService<ILogger<NavigationService>>()
                ));

            services.AddSingleton<IDialogService, Management.Presentation.Services.DialogService>();
            // services.AddSingleton<INotificationService, NotificationService>(); // Moved up for unification
            services.AddSingleton<IOnboardingStateStore, OnboardingStateStore>();
            services.AddSingleton<IStateResettable>(s => (OnboardingStateStore)s.GetRequiredService<IOnboardingStateStore>());
            services.AddSingleton<IViewMappingService, ViewMappingService>();
            services.AddSingleton<IModalNavigationService, ModalNavigationService>();
            
            // --- NAVIGATION ---
            services.AddSingleton<INavigationRegistry, NavigationRegistry>();
            
            // Navigation Strategies
            services.AddSingleton<Services.Navigation.IFacilityNavigationProvider, Services.Navigation.GymNavigationProvider>();
            services.AddSingleton<Services.Navigation.IFacilityNavigationProvider, Services.Navigation.SalonNavigationProvider>();
            services.AddSingleton<Services.Navigation.IFacilityNavigationProvider, Services.Navigation.RestaurantNavigationProvider>();

            // Sync Strategies
            services.AddSingleton<IFacilitySyncStrategy, GymSyncStrategy>();
            services.AddSingleton<IFacilitySyncStrategy, SalonSyncStrategy>();
            services.AddSingleton<IFacilitySyncStrategy, RestaurantSyncStrategy>();

            services.AddSingleton<GlobalExceptionHandler>();


            // --- VIEW MODELS ---
            services.AddSingleton<MainViewModel>();
            services.AddTransient<AuthViewModel>();
            services.AddTransient<TopBarViewModel>();
            services.AddTransient<CommandPaletteViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<SplashOnboardingViewModel>();
            services.AddTransient<LicenseEntryViewModel>();
            services.AddTransient<FacilityOnboardingViewModel>();
            services.AddSingleton<OnboardingOwnerViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<OnboardingOwnerViewModel>());
            services.AddTransient<NotificationDetailViewModel>();
            services.AddTransient<EmailConfirmationViewModel>();
            services.AddTransient<OnboardingViewModel>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<DashboardViewModel>());

            services.AddSingleton<MenuManagementViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<MenuManagementViewModel>());

            services.AddSingleton<GymHomeViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<GymHomeViewModel>());

            services.AddSingleton<SalonHomeViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SalonHomeViewModel>());

            services.AddSingleton<RestaurantHomeViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<RestaurantHomeViewModel>());
            services.AddTransient<FloorPlanViewModel>();
            services.AddTransient<TableDetailViewModel>();

            services.AddSingleton<AddTableViewModel>();
            services.AddSingleton<SelectTableViewModel>();

            services.AddTransient<RestaurantOrderingViewModel>();

            services.AddSingleton<RfidAccessControlViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<RfidAccessControlViewModel>());

            services.AddSingleton<MembersViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<MembersViewModel>());

            services.AddSingleton<RegistrationsViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<RegistrationsViewModel>());

            services.AddSingleton<HistoryViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<HistoryViewModel>());

            services.AddSingleton<FinanceAndStaffViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<FinanceAndStaffViewModel>());

            services.AddTransient<Management.Presentation.ViewModels.Finance.AddStaffViewModel>();
            services.AddTransient<SalonAddStaffViewModel>();

            services.AddSingleton<ShopViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<ShopViewModel>());

            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SettingsViewModel>());

            services.AddSingleton<DeviceManagementViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<DeviceManagementViewModel>());
            
            services.AddTransient<Lazy<DeviceManagementViewModel>>(s => new Lazy<DeviceManagementViewModel>(s.GetRequiredService<DeviceManagementViewModel>));

            services.AddSingleton<AppointmentsViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<AppointmentsViewModel>());

            services.AddSingleton<ServicesViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<ServicesViewModel>());

            services.AddTransient<PromotionEditorViewModel>();
            services.AddTransient<PromotionViewModel>();

            services.AddSingleton<SchedulerViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SchedulerViewModel>());

            services.AddSingleton<BookingViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<BookingViewModel>());
            services.AddTransient<MemberDetailViewModel>();
            services.AddTransient<ProductDetailViewModel>();
            services.AddTransient<CheckoutViewModel>();
            services.AddTransient<AddProductViewModel>();
            services.AddTransient<Management.Presentation.ViewModels.Sync.ConflictResolutionViewModel>();
            services.AddTransient<QuickSaleViewModel>();
            services.AddTransient<QuickRegistrationViewModel>();
            services.AddTransient<MemberAccessViewModel>();
            services.AddTransient<MultiSaleCartViewModel>();
            services.AddTransient<WalkInConfirmationViewModel>();
            services.AddTransient<RegisterWalkInViewModel>();
            services.AddTransient<ChangeFacilityViewModel>();
            services.AddTransient<FacilityAuthViewModel>();
            services.AddTransient<SessionExpiredViewModel>();
            services.AddTransient<LogoutConfirmationViewModel>();
            services.AddTransient<ConfirmationModalViewModel>();
            services.AddTransient<MembershipPlanEditorViewModel>();
            services.AddTransient<SalonServiceEditorViewModel>();
            services.AddTransient<MenuItemEditorViewModel>();
            services.AddTransient<AppointmentDetailViewModel>();
            services.AddTransient<PayrollViewModel>();
            services.AddTransient<PayrollHistoryViewModel>();
            services.AddTransient<RevenueHistoryViewModel>();
            services.AddTransient<OccupancyHistoryViewModel>();
            services.AddTransient<InventoryHistoryViewModel>();
            services.AddTransient<LogRestockViewModel>();
            services.AddTransient<DiscountEditorViewModel>();
            services.AddTransient<AppExitViewModel>();

            // --- VIEWS ---
            services.AddTransient<AuthWindow>();
            services.AddTransient<Management.Presentation.Views.Shell.MainWindow>(s => new Management.Presentation.Views.Shell.MainWindow(s.GetRequiredService<MainViewModel>()));
            services.AddTransient<ConflictResolutionView>();
            services.AddTransient<Views.Auth.LoginView>();
            services.AddTransient<Views.Auth.SplashOnboardingView>();
            services.AddTransient<BookingModal>();
            services.AddTransient<CompletionModal>();
            services.AddTransient<AppointmentDetailModal>();
            services.AddTransient<RevenueHistoryView>();
            services.AddTransient<OccupancyHistoryView>();
            services.AddTransient<InventoryHistoryView>();
            services.AddTransient<LogRestockView>();
            services.AddTransient<LogoutConfirmationWindow>();
        }

        private async System.Threading.Tasks.Task InitializeDatabaseAsync(CancellationToken ct = default)
        {
            try
            {
                // Phase 1: Check for cancellation
                ct.ThrowIfCancellationRequested();

                using (var scope = ServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var provider = dbContext.Database.ProviderName;
                    
                    Serilog.Log.Information("Starting database initialization (Provider: {Provider})...", provider);
                    
                    // Phase 4: Standardize on EF Core Migrations
                    // MigrateAsync handles both creation and schema updates safely.
                    try 
                    {
                        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                        var dbMode = Configuration["Database:Mode"] ?? "LocalFirst";
                        
                        if (!isDev || dbMode != "LocalFirst")
                        {
                            Serilog.Log.Information("Ensuring legacy non-EF tables exist...");
                            await dbContext.Database.ExecuteSqlRawAsync(@"
                                -- ============================================================
                                -- INVENTORY TABLES (Legacy raw SQL, not yet in EF model)
                                -- ============================================================
                                CREATE TABLE IF NOT EXISTS inventory_resources (
                                    id text PRIMARY KEY,
                                    tenant_id text,
                                    facility_id text,
                                    name text NOT NULL,
                                    unit text NOT NULL,
                                    created_at text,
                                    updated_at text
                                );
                                CREATE TABLE IF NOT EXISTS inventory_purchases (
                                    id text PRIMARY KEY,
                                    tenant_id text,
                                    facility_id text,
                                    resource_id text REFERENCES inventory_resources(id) ON DELETE CASCADE,
                                    quantity numeric NOT NULL DEFAULT 0,
                                    total_price numeric NOT NULL DEFAULT 0,
                                    unit_price numeric NOT NULL DEFAULT 0,
                                    date text,
                                    note text,
                                    created_at text
                                );
                            ", ct);
                        }

                        // PRE-MIGRATION BACKUP
                        try
                        {
                            var backupService = scope.ServiceProvider.GetRequiredService<Management.Infrastructure.Services.IBackupService>();
                            await backupService.CreateBackupAsync();
                            Serilog.Log.Information("Pre-migration backup created successfully.");
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Warning(ex, "Pre-migration backup failed. Proceeding with migration.");
                        }

                        await dbContext.Database.MigrateAsync(ct);
                        Serilog.Log.Information("Database migration successful.");
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Error during MigrateAsync. Database may be in a legacy state.");
                        throw;
                    }
                    
                    // Phase 4: Execute WAL and runtime data-healing only
                    await dbContext.EnsureDatabaseSchemaAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw; // propagate
            }
            catch (Exception ex)
            {
                // We log this but don't crash, in case the app can run in "Offline Mode" later
                Serilog.Log.Fatal(ex, "Database migration failed");
                throw; // Rethrow to be handled by caller
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // CRASH INVESTIGATION LOGGING
            var crashLog = $"[FATAL CRASH] DispatcherUnhandledException: {e.Exception.Message}\nType: {e.Exception.GetType().FullName}\nStack Trace: {e.Exception.StackTrace}\nInner Exception: {e.Exception.InnerException?.Message}\nInner Stack Trace: {e.Exception.InnerException?.StackTrace}";
            Serilog.Log.Fatal(crashLog);
            File.WriteAllText("crash-debug-dispatcher.txt", crashLog);

            ReportErrorToDiagnostics("Dispatcher", e.Exception, Management.Application.Services.DiagnosticSeverity.Critical);
            
            Serilog.Log.Fatal(e.Exception, "Unhandled Dispatcher Exception");

            if (!_isHandlingException)
            {
                _isHandlingException = true;
                ShowDiagnosticWindow(e.Exception);
            }

            e.Handled = true;
            RecordFatalCrash("DispatcherUnhandledException", e.Exception);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // CRASH INVESTIGATION LOGGING
            var crashLog = $"[FATAL CRASH] UnobservedTaskException: {e.Exception.Message}\nType: {e.Exception.GetType().FullName}\nStack Trace: {e.Exception.StackTrace}";
            Serilog.Log.Error(crashLog);
            File.WriteAllText("crash-debug-task.txt", crashLog);

            e.SetObserved(); // Set Observed immediately to prevent finalizer crash
            ReportErrorToDiagnostics("Background Task", e.Exception, Management.Application.Services.DiagnosticSeverity.Error);
            Serilog.Log.Error(e.Exception, "Unobserved Task Exception");
            RecordFatalCrash("UnobservedTaskException", e.Exception);
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown AppDomain Exception");
            
            // CRASH INVESTIGATION LOGGING
            var crashLog = $"[FATAL CRASH] AppDomainUnhandledException (Terminating: {e.IsTerminating}): {ex.Message}\nType: {ex.GetType().FullName}\nStack Trace: {ex.StackTrace}";
            Serilog.Log.Fatal(crashLog);
            File.WriteAllText("crash-debug-appdomain.txt", crashLog);

            ReportErrorToDiagnostics("AppDomain", ex, Management.Application.Services.DiagnosticSeverity.Critical);
            Serilog.Log.Fatal(ex, "Unhandled AppDomain Exception");

            if (!_isHandlingException && e.IsTerminating)
            {
                _isHandlingException = true;
                ShowDiagnosticWindow(ex);
            }
            RecordFatalCrash("AppDomainUnhandledException", ex);
        }

        private void RecordFatalCrash(string type, Exception? ex)
        {
            try
            {
                var luxuryaFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luxurya");
                if (!Directory.Exists(luxuryaFolder)) Directory.CreateDirectory(luxuryaFolder);
                string logPath = Path.Combine(luxuryaFolder, "crash_log.txt");
                string content = $"\n\n[{DateTime.Now}] FATAL CRASH: {type}\n" +
                                 $"Exception: {ex?.GetType().Name}\n" +
                                 $"Message: {ex?.Message}\n" +
                                 $"Stack Trace:\n{ex?.StackTrace}\n" +
                                 (ex?.InnerException != null ? $"Inner Exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n" : "") +
                                 "--------------------------------------------------\n";
                
                System.IO.File.AppendAllText(logPath, content);
                
                // Also force it to console for debugging
                Console.WriteLine(content);
            }
            catch { /* Infinite recursion prevention */ }
        }

        private void ReportErrorToDiagnostics(string context, Exception ex, Management.Application.Services.DiagnosticSeverity severity)
        {
            // Phase 1: Guard against disposed ServiceProvider
            if (_isServiceProviderDisposed || ServiceProvider == null)
            {
                // Fallback: Log to Serilog only when ServiceProvider unavailable
                Serilog.Log.Error(ex, "[{Context}] Error after ServiceProvider disposal: {Message}", context, ex.Message);
                return;
            }

            try
            {
                var diagnosticService = ServiceProvider.GetService<Management.Application.Services.IDiagnosticService>();
                diagnosticService?.LogError(
                    Management.Application.Services.DiagnosticCategory.Runtime,
                    context,
                    ex.Message,
                    ex,
                    severity
                );
            }
            catch (ObjectDisposedException)
            {
                _isServiceProviderDisposed = true;
                Serilog.Log.Error(ex, "[{Context}] ServiceProvider was disposed during error reporting", context);
            }
            catch (Exception innerEx)
            {
                // Defensive: If error reporting itself fails, log to Serilog
                Serilog.Log.Error(innerEx, "[{Context}] Failed to report error to diagnostics", context);
                Serilog.Log.Error(innerEx, "[{Context}] Original error", context);
            }
        }

        private async Task CheckClockDriftAsync()
        {
            try
            {
                var storage = ServiceProvider.GetRequiredService<Management.Application.Services.ISecureStorageService>();
                var url = storage.Get("SupabaseUrl") ?? Configuration["Supabase:Url"];
                
                if (string.IsNullOrEmpty(url)) return;

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                
                // Use HEAD to get just the headers (minimal bandwidth)
                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                
                if (response.Headers.Date.HasValue)
                {
                    var serverTime = response.Headers.Date.Value.UtcDateTime;
                    var localTime = DateTime.UtcNow;
                    var drift = (serverTime - localTime).Duration();

                    Serilog.Log.Information("[App] Clock Drift Check: Server={Server}, Local={Local}, Drift={Drift}", serverTime, localTime, drift);

                    if (drift > TimeSpan.FromMinutes(5))
                    {
                        var error = $"CRITICAL: System clock drift detected ({drift.TotalMinutes:F1} minutes). " +
                                    "Please synchronize your PC clock with Internet time to prevent data corruption and sync failures.";
                        Serilog.Log.Fatal(error);
                        throw new Exception(error);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Warning(ex, "[App] Clock drift check skipped: could not reach Supabase.");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                if (ex.Message.Contains("CRITICAL: System clock drift detected")) throw;
                Serilog.Log.Warning(ex, "[App] Clock drift check failed due to unexpected error.");
            }
        }

        private void ShowDiagnosticWindow(Exception ex)
        {
            Current.Dispatcher.InvokeAsync(() => {
                try 
                {
                    var diagnosticViewModel = ServiceProvider?.GetService<DiagnosticViewModel>();
                    if (diagnosticViewModel != null)
                    {
                        var diagnosticWindow = new Views.Diagnostic.DiagnosticView(diagnosticViewModel);
                        diagnosticWindow.Show();
                    }
                    else 
                    {
                        MessageBox.Show($"A critical error occurred:\n\n{ex.Message}\n\nCheck logs for details.",
                            "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch 
                {
                    MessageBox.Show($"A critical error occurred:\n\n{ex.Message}\n\nCheck logs for details.",
                        "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        /// <summary>
        /// Switches the application theme between Light and Dark modes.
        /// </summary>
        /// <param name="isDarkMode">True for dark mode, false for light mode.</param>
        public void SetTheme(bool isDarkMode)
        {
            // FIX 1: Wrapped in Dispatcher.InvokeAsync to ensure UI thread safety.
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var themeName = isDarkMode ? "Theme.Dark.xaml" : "Theme.Light.xaml";
                    // FIX 1: Use absolute pack URI — relative URIs break in installed (published) builds.
                    var themeUri = new Uri(
                        $"pack://application:,,,/Luxurya.Client;component/Resources/{themeName}",
                        UriKind.Absolute);

                    // Find and remove existing theme dictionary
                    var existingTheme = Resources.MergedDictionaries
                        .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Theme.Dark.xaml") == true ||
                                            d.Source?.OriginalString?.Contains("Theme.Light.xaml") == true);

                    if (existingTheme != null)
                    {
                        Resources.MergedDictionaries.Remove(existingTheme);
                    }

                    // Add new theme dictionary at the end (so it has highest priority)
                    var newTheme = new ResourceDictionary { Source = themeUri };
                    Resources.MergedDictionaries.Add(newTheme);

                    Serilog.Log.Information($"Theme switched to: {themeName}");
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to switch theme");
                }
            });
        }
        protected override async void OnExit(System.Windows.ExitEventArgs e)
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();

            Serilog.Log.Information("[App] Shutdown initiated. Starting sync-on-exit...");
            
            try
            {
                var syncService = _host.Services.GetRequiredService<Management.Application.Interfaces.App.ISyncService>();
                var outboxCount = await syncService.GetPendingOutboxCountAsync();
                
                if (outboxCount > 0)
                {
                    Serilog.Log.Information("[App] Pending outbox items detected ({Count}). Blocking for final sync...", outboxCount);
                    
                    // Note: This is an async void override, so we can't truly block the OS from killing us, 
                    // but on Windows WPF, this gives us a window before the process terminates.
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await syncService.PushChangesAsync(cts.Token);
                    Serilog.Log.Information("[App] Final sync complete.");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[App] Error during final sync on exit.");
            }

            try
            {
                Serilog.Log.Information("Application shutdown initiated...");
                
                // Phase 2: Clear SQLite Pools to release file locks immediately
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                
                // Phase 1: Signal all background services to stop
                _appShutdownCts.Cancel();
                
                // Phase 4: Stop Host (Gracefully stops all IHostedServices)
                if (_host != null)
                {
                    Serilog.Log.Information("Stopping Host and background services...");
                    try
                    {
                        // Wait up to 5 seconds for graceful shutdown
                        // REFACTORED: Properly await stop before disposal
                        await _host.StopAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Error during Host shutdown");
                    }
                    finally
                    {
                        // Phase 1: Mark ServiceProvider as about to be disposed
                        _isServiceProviderDisposed = true;

                        // Phase 4: Dispose Host (also disposes ServiceProvider)
                        Serilog.Log.Information("Disposing Host...");
                        _host.Dispose();
                    }
                }

                Serilog.Log.Information("Application shutdown complete.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "Error during application shutdown");
            }
            finally
            {
                Serilog.Log.CloseAndFlush();
                base.OnExit(e);
            }
        }
        private async Task<bool> RunStartupSecurityGuard(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                var onboardingService = scope.ServiceProvider.GetRequiredService<IOnboardingService>();
                var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                var supabase = scope.ServiceProvider.GetRequiredService<Supabase.Client>();
                var hardwareService = scope.ServiceProvider.GetRequiredService<IHardwareService>();

                Serilog.Log.Information("Startup Security Guard: Verifying device license...");

                try
                {
                    // Use the new hardened verification with offline fallback
                    // Now returns the TenantId directly if verified via RPC
                    var verificationResult = await onboardingService.VerifyCurrentDeviceAsync();
                    
                    if (verificationResult.IsFailure)
                    {
                        Serilog.Log.Warning("[App] Device verification failed (Network/System Error). Proceeding to activation.");
                        return false;
                    }

                    // For online check, we get the TenantId back
                    if (verificationResult.Value.HasValue)
                    {
                        var tenantId = verificationResult.Value.Value;
                        tenantService.SetTenantId(tenantId);
                        
                        // Fix: Explicitly flag expansion flow in state store so Router skips the owner check
                        var stateStore = scope.ServiceProvider.GetRequiredService<IOnboardingStateStore>();
                        stateStore.TargetTenantId = tenantId;
                        stateStore.IsExpansionFlow = true;

                        Serilog.Log.Information($"[App] Device verified via RPC. Tenant context set to {tenantId}. Expansion Flow flagged.");
                        return true;
                    }

                    // If Value is null, it means either offline lease found it OR no binding exists
                    var hardwareId = hardwareService.GetHardwareId();
                    var lease = await _host!.Services.GetRequiredService<IConfigurationService>().LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                    
                    if (lease != null && lease.IsValid(hardwareId))
                    {
                        try 
                        {
                            var configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Luxurya", "facility-config.json");
                            if (System.IO.File.Exists(configPath))
                            {
                                var jsonStr = System.IO.File.ReadAllText(configPath);
                                using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                                if (doc.RootElement.TryGetProperty("TenantId", out var tProp) && tProp.TryGetGuid(out var tId))
                                {
                                    tenantService.SetTenantId(tId);
                                    scope.ServiceProvider.GetRequiredService<IOnboardingStateStore>().TargetTenantId = tId;
                                    Serilog.Log.Information($"[App] Device verified via local lease (Offline). Tenant context set to {tId}");
                                    return true;
                                }
                            }
                        } catch { }

                        // Fallback if TenantId isn't found
                        Serilog.Log.Information($"[App] Device verified via local lease (Offline), but no TenantId found in config. Using Empty.");
                        return true;
                    }

                    Serilog.Log.Warning("[App] Device verification failed (No server binding or local lease). Proceeding to activation.");
                    return false;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error during Startup Security Guard check");
                    return false;
                }
            }
        }

        private void PopulateNavigationRegistry(INavigationRegistry registry)
        {
            // --- GLOBAL HOME VIEWS ---
            registry.RegisterHomeView<GymHomeViewModel>(Domain.Enums.FacilityType.Gym);
            registry.RegisterHomeView<SalonHomeViewModel>(Domain.Enums.FacilityType.Salon);
            registry.RegisterHomeView<RestaurantHomeViewModel>(Domain.Enums.FacilityType.Restaurant);

            // --- GYM ---
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Home", "Terminology.Sidebar.Home", "Icon.Home", typeof(GymHomeViewModel), 0));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Dashboard", "Terminology.Sidebar.Dashboard", "Icon.ChartBar", typeof(DashboardViewModel), 1, Management.Domain.Constants.SystemPermissions.ViewDashboard));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Members", "Terminology.Sidebar.Members", "Icon.Members", typeof(MembersViewModel), 2, Management.Domain.Constants.SystemPermissions.CreateMember));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Registrations", "Terminology.Sidebar.Registrations", "Icon.RegistrationForm", typeof(Management.Presentation.ViewModels.Registrations.RegistrationsViewModel), 3));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("History", "Terminology.Sidebar.History", "Icon.HistoryRewind", typeof(HistoryViewModel), 4));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Staff", "Terminology.Sidebar.Staff", "Icon.StaffBadge", typeof(Management.Presentation.ViewModels.Finance.FinanceAndStaffViewModel), 5, Management.Domain.Constants.SystemPermissions.CreateStaff));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Shop", "Terminology.Sidebar.Shop", "IconShop", typeof(ShopViewModel), 6, Management.Domain.Constants.SystemPermissions.ModifyProduct));

            // --- SALON ---
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Home", "Terminology.Sidebar.Home", "Icon.Home", typeof(SalonHomeViewModel), 0));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Dashboard", "Terminology.Sidebar.Dashboard", "Icon.ChartBar", typeof(DashboardViewModel), 1, Management.Domain.Constants.SystemPermissions.ViewDashboard));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Schedule", "Terminology.Sidebar.Schedule", "Icon.Appointments", typeof(AppointmentsViewModel), 2));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Clients", "Terminology.Sidebar.Clients", "Icon.Members", typeof(MembersViewModel), 3, Management.Domain.Constants.SystemPermissions.CreateMember));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Bookings", "Terminology.Sidebar.Registrations", "Icon.RegistrationForm", typeof(RegistrationsViewModel), 4));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Staff", "Terminology.Sidebar.Staff", "Icon.StaffBadge", typeof(FinanceAndStaffViewModel), 5, Management.Domain.Constants.SystemPermissions.CreateStaff));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("History", "Terminology.Sidebar.History", "Icon.HistoryRewind", typeof(HistoryViewModel), 6));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Shop", "Terminology.Sidebar.Shop", "IconShop", typeof(ShopViewModel), 7, Management.Domain.Constants.SystemPermissions.ModifyProduct));

            // --- RESTAURANT ---
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Home", "Terminology.Sidebar.Home", "Icon.Home", typeof(RestaurantHomeViewModel), 0));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Dashboard", "Terminology.Sidebar.Dashboard", "Icon.ChartBar", typeof(DashboardViewModel), 1, Management.Domain.Constants.SystemPermissions.ViewDashboard));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Floor Plan", "Terminology.Sidebar.FloorPlan", "IconDashboard", typeof(FloorPlanViewModel), 2));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Menu", "Terminology.Settings.Menu", "IconShop", typeof(MenuManagementViewModel), 3));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("History", "Terminology.Sidebar.History", "Icon.HistoryRewind", typeof(HistoryViewModel), 4));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Staff", "Terminology.Sidebar.Staff", "Icon.StaffBadge", typeof(Management.Presentation.ViewModels.Finance.FinanceAndStaffViewModel), 5, Management.Domain.Constants.SystemPermissions.CreateStaff));

            // --- GENERAL (Neutral fallback) ---
            registry.Register(Domain.Enums.FacilityType.General, new NavigationItemMetadata("Home", "Terminology.Sidebar.Home", "Icon.Home", typeof(DashboardViewModel), 0, Management.Domain.Constants.SystemPermissions.ViewDashboard));
        }

        private void UpdateStartupStatus(string status)
        {
            Current.Dispatcher.InvokeAsync(() => 
            {
                var navStore = ServiceProvider?.GetService<NavigationStore>();
                if (navStore?.CurrentViewModel is LoginViewModel loginVm)
                {
                    loginVm.AppInitializationStatus = status;
                }
            });
        }
    }
}
