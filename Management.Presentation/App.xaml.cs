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
using Management.Presentation.Views.Shared;

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
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                if (currentExe == null) return false;

                var zkDllPath = Path.Combine(Path.GetDirectoryName(currentExe)!, "zkemkeeper.dll");

                if (!File.Exists(zkDllPath))
                {
                    Serilog.Log.Warning("[Hardware] zkemkeeper.dll not found at {Path}", zkDllPath);
                    return false;
                }

                Serilog.Log.Information("[Hardware] Attempting ZKTeco SDK registration (Silent={Silent})...", silent);

                var result = Process.Start(new ProcessStartInfo
                {
                    FileName = "regsvr32.exe",
                    Arguments = $"{(silent ? "/s" : "")} \"{zkDllPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                
                if (result != null)
                {
                    bool exited = result.WaitForExit(10000);
                    if (exited && result.ExitCode == 0)
                    {
                        Serilog.Log.Information("[Hardware] ZKTeco SDK registered successfully via regsvr32");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[Hardware] ZKTeco SDK registration failed — turnstile may not work");
                return false;
            }
        }



        protected override void OnStartup(StartupEventArgs e)
        {


            if (!EnsureSingleInstance()) return;
            
            // Execute startup logic on a separate task to avoid blocking UI thread 
            // but with robust error handling and proper synchronization.
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
                // 1. Global Exception Handling (Register EARLY)
                this.DispatcherUnhandledException += OnDispatcherUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

                // 2. Setup Logging (Serilog)
                Serilog.Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                Serilog.Log.Information("APPLICATION STARTUP BEGIN ==========================================");

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
                
                // FIX 12: Check Clock Drift before anything else
                UpdateStartupStatus("Checking system clock...");
                await CheckClockDriftAsync();

                // 1. Initialize Contexts (Must happen before Sync)
                Serilog.Log.Information("Initializing Facility Context and Resilience...");
                var facilityContext = ServiceProvider.GetRequiredService<IFacilityContextService>();
                await Task.Run(() => facilityContext.Initialize());

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
                await Current.Dispatcher.InvokeAsync(async () => {
                    try 
                    {
                        var authVm = ServiceProvider.GetRequiredService<AuthViewModel>();
                        var authWindow = ServiceProvider.GetRequiredService<AuthWindow>();
                        var navService = ServiceProvider.GetRequiredService<INavigationService>();

                        authWindow.DataContext = authVm;
                        Current.MainWindow = authWindow;
                        authWindow.Show();
                        
                        // Default to Login view
                        await navService.NavigateToLoginAsync();
                        
                        var navStore = ServiceProvider.GetRequiredService<NavigationStore>();
                        if (navStore.CurrentViewModel is LoginViewModel loginVm)
                        {
                            loginVm.SetInitializingState(true);
                            loginVm.AppInitializationStatus = "Starting application services...";
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to show initial AuthWindow");
                    }
                });

                // 4. Background Database Initialization
                Serilog.Log.Information("[App] Initializing database schema in background...");
                UpdateStartupStatus("Initializing database...");
                var dbInitTask = InitializeDatabaseAsync(ct);
                await dbInitTask; 

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

                // (Localization already initialized early)

                // Synchronize SessionManager with the committed facility context
                var sessionManager = ServiceProvider.GetRequiredService<Management.Presentation.Services.State.SessionManager>();
                sessionManager.CurrentFacility = facilityContext.CurrentFacility;


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
                // SelectTableViewModel and OpenOrdersViewModel are now UserControls handled via DataTemplates in App.xaml
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
                
                // 5. DB Init already completed above (moved before auto-discovery).
                ct.ThrowIfCancellationRequested();
                Serilog.Log.Information("[App] Database schema already initialized.");

                // (Localization moved to earlier in sequence)
                // FIX 2: Removed duplicate PopulateNavigationRegistry call here — already called at step 4 above.
                
                // 6. Startup Security Guard (Hardware/Cloud Check)
                // This was already moved lower in my previous thought but let's be explicit.
                ct.ThrowIfCancellationRequested();
                Serilog.Log.Information("Running Startup Security Guard in background...");
                UpdateStartupStatus("Verifying license...");
                bool isLicensed = await RunStartupSecurityGuard(ServiceProvider);

                // 7. FINAL NAVIGATION ROUTING (Based on background task results)
                await Current.Dispatcher.InvokeAsync(async () => {
                    try 
                    {
                        var navService = ServiceProvider.GetRequiredService<INavigationService>();
                        var navStore = ServiceProvider.GetRequiredService<NavigationStore>();

                        if (!isLicensed)
                        {
                            Serilog.Log.Information("Device not licensed. Navigating to Activation...");
                            await navService.NavigateToAsync<LicenseEntryViewModel>();
                        }
                        else
                        {
                            var stateStore = ServiceProvider.GetRequiredService<IOnboardingStateStore>();
                            if (stateStore.TargetTenantId.HasValue)
                            {
                                var authService = ServiceProvider.GetRequiredService<IAuthenticationService>();
                                bool hasOwner = await authService.TenantHasOwnerAccountAsync(stateStore.TargetTenantId.Value);

                                if (hasOwner)
                                {
                                    Serilog.Log.Information("Tenant already has owner. Navigating to Login.");
                                    await navService.NavigateToLoginAsync();
                                }
                                else
                                {
                                    await navService.NavigateToAsync<FacilityOnboardingViewModel>();
                                }
                            }
                            else
                            {
                                // Natural state: clear initialization flags on Current ViewModel if it's Login
                                if (navStore.CurrentViewModel is LoginViewModel loginVm)
                                {
                                    loginVm.SetInitializingState(false);
                                    loginVm.AppInitializationStatus = string.Empty;
                                }
                                Serilog.Log.Information("Background startup: Initialization complete.");

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
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Error during final navigation routing");
                    }
                });
                
                Serilog.Log.Information("Startup Sequence Complete.");
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
                    try 
                    {
                        var resettables = ServiceProvider.GetServices<Management.Domain.Interfaces.IStateResettable>();
                        foreach (var resettable in resettables)
                        {
                            resettable.ResetState();
                        }

                        // Re-synchronize SessionManager after reset
                        var facilityContext = ServiceProvider.GetRequiredService<Management.Domain.Services.IFacilityContextService>();
                        var sessionManager = ServiceProvider.GetRequiredService<Management.Presentation.Services.State.SessionManager>();
                        sessionManager.CurrentFacility = facilityContext.CurrentFacility;

                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to reset state during LaunchMainWindow");
                    }

                    var mainWindow = ServiceProvider.GetRequiredService<Management.Presentation.Views.Shell.MainWindow>();
                    var oldWindow = Current.MainWindow;

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
                    var authVm = ServiceProvider.GetRequiredService<AuthViewModel>();
                    var authWindow = ServiceProvider.GetRequiredService<AuthWindow>();
                    var navService = ServiceProvider.GetRequiredService<INavigationService>();
                    var oldWindow = Current.MainWindow;

                    authWindow.DataContext = authVm;
                    Current.MainWindow = authWindow;
                    authWindow.Show();

                    oldWindow?.Close();

                    // Navigate to Login view within Auth shell
                    await navService.NavigateToLoginAsync();
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
                        MessageBox.Show($"Fatal Error during startup:\n\n{ex.Message}", "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch 
                {
                    MessageBox.Show($"Fatal Error during startup:\n\n{ex.Message}", "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luxurya");
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
            
            // Register Home ViewModels and Shell ViewModels as Resettable
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<MainViewModel>());

            // --- DOMAIN SERVICES (Business Logic) ---
            services.AddTransient<IMembershipService, MembershipService>();
            services.AddTransient<IMemberService, MemberService>();
            services.AddTransient<IStaffService, StaffService>();
            services.AddTransient<IRegistrationService, RegistrationService>();
            services.AddTransient<IWebsiteRegistrationService, WebsiteRegistrationService>();
            services.AddTransient<IAccessEventService, AccessEventService>();
            services.AddTransient<IProductService, ProductService>();
            services.AddTransient<ISaleService, SaleService>();
            services.AddTransient<IReservationService, ReservationService>();
            services.AddTransient<IMembershipPlanService, MembershipPlanService>();
            services.AddTransient<ISessionMonitorService, SessionMonitorService>();
            services.AddSingleton<Management.Domain.Services.IEmailService, Management.Infrastructure.Services.NullEmailService>();
            // Added Missing Domain Services
            services.AddTransient<Management.Application.Interfaces.App.IGymOperationService, Management.Application.Services.GymOperationService>();
            services.AddSingleton<Management.Application.Interfaces.App.IAudioService, Management.Infrastructure.Services.Audio.AudioService>();
            services.AddSingleton<IAccessControlCache, AccessControlCache>();
            services.AddTransient<ITableService, TableService>();
            services.AddTransient<IAccessControlService, AccessControlService>();
            // The line below was moved up as part of the change.
            // services.AddSingleton<IAccessControlCache, AccessControlCache>();

            // --- APPLICATION SERVICES (Orchestration) ---
            services.AddSingleton<Management.Domain.Services.IConnectionService, ConnectionService>();
            services.AddTransient<IAuthenticationService, AuthenticationService>();
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

            services.AddTransient<IDashboardService, DashboardService>();
            services.AddTransient<ITransactionService, TransactionService>();
            services.AddSingleton<ISecurityService, SecurityService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddTransient<IReportingService, ReportingService>();
            services.AddSingleton<Management.Application.Services.ISecureStorageService, Management.Presentation.Services.Infrastructure.SecureStorageService>();

            // --- DIAGNOSTIC SYSTEM ---
            services.AddSingleton<Management.Application.Services.IDiagnosticService, Management.Presentation.Services.DiagnosticService>();
            services.AddSingleton<DiagnosticViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<DiagnosticViewModel>());
            services.AddSingleton<ConnectivityViewModel>(); // Shared VM for banner

            // Sync Engine
            services.AddSingleton<Management.Application.Interfaces.App.ISyncService, SyncService>();
            services.AddSingleton<ISyncEventDispatcher, SyncEventDispatcher>();
            services.AddHostedService<SyncWorker>();
            
            services.AddSingleton<SupabaseRealtimeService>();
            services.AddHostedService(provider => provider.GetRequiredService<SupabaseRealtimeService>());
            
            services.AddHostedService<AccessMonitoringWorker>();

            // History Providers
            services.AddTransient<Management.Application.Interfaces.App.IHistoryProvider, Management.Application.Services.History.GymHistoryProvider>();
            services.AddTransient<Management.Application.Interfaces.App.IHistoryProvider, Management.Application.Services.History.SalonHistoryProvider>();
            services.AddTransient<Management.Application.Interfaces.App.IHistoryProvider, Management.Application.Services.History.RestaurantHistoryProvider>();


            // --- PRESENTATION SERVICES (UI) ---
            var wpfDispatcher = new WpfDispatcher(System.Windows.Application.Current.Dispatcher);
            services.AddSingleton<IDispatcher>(wpfDispatcher);
            services.AddSingleton<IDispatcherService>(wpfDispatcher);
            
            // Session and User Management
            services.AddSingleton<SessionManager>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SessionManager>());
            services.AddSingleton<Management.Application.Interfaces.App.IToastService, ToastService>();
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            
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
            services.AddSingleton<IToastNotificationService, ToastNotificationService>();
            services.AddSingleton<INotificationService, NotificationService>();
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

            services.AddSingleton<Management.Presentation.ViewModels.Finance.AddStaffViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<Management.Presentation.ViewModels.Finance.AddStaffViewModel>());
            services.AddSingleton<SalonAddStaffViewModel>();
            services.AddSingleton<IStateResettable>(s => s.GetRequiredService<SalonAddStaffViewModel>());

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
            services.AddTransient<ChangeFacilityViewModel>();
            services.AddTransient<FacilityAuthViewModel>();
            services.AddTransient<SessionExpiredViewModel>();
            services.AddTransient<ConfirmationModalViewModel>();
            services.AddTransient<MembershipPlanEditorViewModel>();
            services.AddTransient<SalonServiceEditorViewModel>();
            services.AddTransient<MenuItemEditorViewModel>();
            services.AddTransient<AppointmentDetailViewModel>();
            services.AddTransient<PayrollViewModel>();
            services.AddTransient<PayrollHistoryViewModel>();

            // --- VIEWS ---
            services.AddTransient<AuthWindow>();
            services.AddTransient<Management.Presentation.Views.Shell.MainWindow>(s => new Management.Presentation.Views.Shell.MainWindow(s.GetRequiredService<MainViewModel>()));
            services.AddTransient<ConflictResolutionView>();
            services.AddTransient<Views.Auth.LoginView>();
            services.AddTransient<BookingModal>();
            services.AddTransient<CompletionModal>();
            services.AddTransient<AppointmentDetailModal>();
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
                        // REFACTORED: Fire and forget to avoid UI thread blocking during shutdown
                        _ = _host.StopAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Error during Host shutdown");
                    }
                }

                // Phase 1: Mark ServiceProvider as about to be disposed
                _isServiceProviderDisposed = true;
                
                // Phase 4: Dispose Host (also disposes ServiceProvider)
                if (_host != null)
                {
                    Serilog.Log.Information("Disposing Host...");
                    _host.Dispose();
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
                        Serilog.Log.Information($"[App] Device verified via RPC. Tenant context set to {tenantId}");
                        return true;
                    }

                    // If Value is null, it means either offline lease found it OR no binding exists
                    var hardwareId = hardwareService.GetHardwareId();
                    var lease = await _host!.Services.GetRequiredService<IConfigurationService>().LoadConfigAsync<Management.Domain.Models.LicenseLease>("license.lease");
                    
                    if (lease != null && lease.IsValid(hardwareId))
                    {
                        Serilog.Log.Information("[App] Device verified via local lease (Offline). WARNING: Tenant context may be limited.");
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
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Dashboard", "Terminology.Sidebar.Dashboard", "Icon.TrendingUp", typeof(DashboardViewModel), 1));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Members", "Terminology.Sidebar.Members", "Icon.Users", typeof(MembersViewModel), 2));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Registrations", "Terminology.Sidebar.Registrations", "Icon.UserCheck", typeof(Management.Presentation.ViewModels.Registrations.RegistrationsViewModel), 3));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("History", "Terminology.Sidebar.History", "Icon.Clock", typeof(HistoryViewModel), 4));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Staff", "Terminology.Sidebar.Staff", "Icon.Users", typeof(Management.Presentation.ViewModels.Finance.FinanceAndStaffViewModel), 5));
            registry.Register(Domain.Enums.FacilityType.Gym, new NavigationItemMetadata("Shop", "Terminology.Sidebar.Shop", "Icon.Storefront", typeof(ShopViewModel), 6));

            // --- SALON ---
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Home", "Terminology.Sidebar.Home", "Icon.Home", typeof(SalonHomeViewModel), 0));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Dashboard", "Terminology.Sidebar.Dashboard", "Icon.TrendingUp", typeof(DashboardViewModel), 1));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Schedule", "Terminology.Sidebar.Schedule", "Icon.Calendar", typeof(AppointmentsViewModel), 2));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Clients", "Terminology.Sidebar.Clients", "Icon.Users", typeof(MembersViewModel), 3));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Bookings", "Terminology.Sidebar.Registrations", "Icon.UserCheck", typeof(RegistrationsViewModel), 4));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Staff", "Terminology.Sidebar.Staff", "Icon.Users", typeof(FinanceAndStaffViewModel), 5));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("History", "Terminology.Sidebar.History", "Icon.Clock", typeof(HistoryViewModel), 6));
            registry.Register(Domain.Enums.FacilityType.Salon, new NavigationItemMetadata("Shop", "Terminology.Sidebar.Shop", "Icon.Storefront", typeof(ShopViewModel), 7));

            // --- RESTAURANT ---
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Home", "Terminology.Sidebar.Home", "Icon.Home", typeof(RestaurantHomeViewModel), 0));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Dashboard", "Terminology.Sidebar.Dashboard", "Icon.TrendingUp", typeof(DashboardViewModel), 1));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Floor Plan", "Terminology.Sidebar.FloorPlan", "IconDashboard", typeof(FloorPlanViewModel), 2));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Menu", "Terminology.Settings.Menu", "IconShop", typeof(MenuManagementViewModel), 3));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("History", "Terminology.Sidebar.History", "Icon.Clock", typeof(HistoryViewModel), 4));
            registry.Register(Domain.Enums.FacilityType.Restaurant, new NavigationItemMetadata("Staff", "Terminology.Sidebar.Staff", "Icon.Users", typeof(Management.Presentation.ViewModels.Finance.FinanceAndStaffViewModel), 5));

            // --- GENERAL (Neutral fallback) ---
            registry.Register(Domain.Enums.FacilityType.General, new NavigationItemMetadata("Home", "Terminology.Sidebar.Home", "Icon.Home", typeof(DashboardViewModel), 0));
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
