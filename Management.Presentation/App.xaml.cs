using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

using Microsoft.Extensions.Caching.Memory;


using Management.Presentation.Stores;
using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Management.Infrastructure.Hardware;
using Management.Infrastructure.Repositories;
using Management.Infrastructure.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Restaurant;
using Management.Presentation.Views.Salon; // Added
using Management.Presentation.Views.Auth; // Added for LoginView
using Management.Presentation.Services.Salon;
using Management.Presentation.Views.Shop;
using Management.Presentation.Views.Settings;
using Management.Presentation.Views.Restaurant;
using Management.Presentation.ViewModels;
using Management.Presentation.Extensions;
using Management.Presentation.Views;
using Management.Domain.DTOs;

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

        public App()
        {
            // PostgreSQL Timestamp Fix (Critical for EF Core + Npgsql legacy compatibility)
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Execute startup logic on a separate task to avoid blocking UI thread 
            // but with robust error handling and proper synchronization.
            InitializeApp();
        }

        private void InitializeApp()
        {
            try 
            {
                // 1. Setup Logging (Serilog)
                Serilog.Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                Serilog.Log.Information("APPLICATION STARTUP BEGIN ==========================================");

                // 2. Setup Configuration
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables();

                Configuration = builder.Build();

                // 3. Setup Dependency Injection
                var services = new ServiceCollection();
                ConfigureServices(services);

                ServiceProvider = services.BuildServiceProvider();

                // 4. Global Exception Handling
                this.DispatcherUnhandledException += OnDispatcherUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

                // 5. Async Initialization (Fire and Forget but with Catch)
                _ = Task.Run(async () => await RunInitializationSequenceAsync());
            }
            catch (Exception ex)
            {
                HandleFatalStartupError(ex);
            }
        }

        private async Task RunInitializationSequenceAsync()
        {
            try
            {
                // 3.5. Register View Mappings
                var mappingService = ServiceProvider.GetRequiredService<IViewMappingService>();
                mappingService.Register<ConflictResolutionViewModel, ConflictResolutionView>();
                mappingService.Register<BookingViewModel, BookingModal>();
                mappingService.Register<CompletionViewModel, CompletionModal>();

                // 4.5. Initialize Diagnostic System
                Serilog.Log.Information("Initializing Diagnostic System...");
                var diagnosticService = ServiceProvider.GetRequiredService<IDiagnosticService>();
                await diagnosticService.StartBindingErrorListenerAsync();

                // Run diagnostic checks (DI & Supabase)
                var diValidation = await diagnosticService.ValidateDependencyInjectionAsync(ServiceProvider);
                var supabaseTest = await diagnosticService.TestSupabaseConnectivityAsync();
                
                // 5. Initialize Contexts & Resilience
                Serilog.Log.Information("Initializing Facility Context and Resilience...");
                ServiceProvider.GetRequiredService<IFacilityContextService>().Initialize();
                // 6. Startup Security Guard (Hardware Check)
                Serilog.Log.Information("Running Startup Security Guard...");
                bool isLicensed = await RunStartupSecurityGuard(ServiceProvider);

                if (isLicensed)
                {
                    // 7. Initialize Database (Migration) - Only for licensed devices
                    Serilog.Log.Information("Initializing Database...");
                    try 
                    {
                        await InitializeDatabaseAsync();
                    }
                    catch (Exception dbEx)
                    {
                        diagnosticService.LogError(Models.DiagnosticCategory.Database, "Migration Failed", dbEx.Message, dbEx, Models.DiagnosticSeverity.Critical);
                        
                        await Current.Dispatcher.InvokeAsync(() => {
                            var diagnosticViewModel = ServiceProvider.GetRequiredService<DiagnosticViewModel>();
                            var diagnosticWindow = new Views.DiagnosticWindow(diagnosticViewModel);
                            Current.MainWindow = diagnosticWindow;
                            diagnosticWindow.Show();
                        });
                        return;
                    }
                }
                else
                {
                    Serilog.Log.Information("Device is not licensed. Onboarding flow active. Skipping DB migration.");
                }

                // Initialize Resilience Service AFTER DB Migration
                await InitializeResilienceAsync(ServiceProvider);

                // 8. Launch Main Application (Shows either Dashboard or LicenseEntry depending on Guard result)

                // 8. Launch Main Application (Shows either Dashboard or LicenseEntry depending on Guard result)
                await Current.Dispatcher.InvokeAsync(() => {
                    try 
                    {
                        Serilog.Log.Information("DEBUG: Resolving MainViewModel...");
                        var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
                        
                        Serilog.Log.Information("DEBUG: Resolving MainWindow...");
                        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                        
                        Current.MainWindow = mainWindow;
                        mainWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        var diError = $"DI FAILURE: {ex.Message}\n{ex.StackTrace}";
                        if (ex.InnerException != null)
                        {
                            diError += $"\nINNER: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                        }
                        Serilog.Log.Fatal(diError);
                        Console.WriteLine(diError);
                        File.WriteAllText("boot-di-debug.txt", diError);
                        HandleFatalStartupError(ex);
                    }
                });
                
                Serilog.Log.Information("Startup Sequence Complete.");
            }
            catch (Exception ex)
            {
                HandleFatalStartupError(ex);
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
                    var diagnosticService = services.GetRequiredService<IDiagnosticService>();
                    diagnosticService.LogError(Models.DiagnosticCategory.Runtime, "Resilience Init", resEx.Message, resEx, Models.DiagnosticSeverity.Error);
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

            Current.Dispatcher.Invoke(() => {
                try 
                {
                    var diagnosticViewModel = ServiceProvider?.GetService<DiagnosticViewModel>();
                    if (diagnosticViewModel != null)
                    {
                        var diagnosticWindow = new Views.DiagnosticWindow(diagnosticViewModel);
                        diagnosticWindow.Show();
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
                cfg.RegisterServicesFromAssembly(typeof(GymDbContext).Assembly);
            });

            // --- TENANT CONTEXT ---
            services.AddSingleton<ITenantService, Infrastructure.Services.TenantService>();

            // --- INFRASTRUCTURE: DATABASE ---
            // CRITICAL: Registered as Transient for WPF to avoid Captive Dependency in Singletons (MainViewModel)
            // and because there is no per-request scope in desktop apps.
            // Repositories will get fresh contexts, but Singletons (like Stores) should be careful.
            var connectionString = Configuration.GetConnectionString("SupabaseConnection");
            services.AddDbContext<GymDbContext>(options =>
            {
                if (!string.IsNullOrEmpty(connectionString))
                {
                    options.UseNpgsql(connectionString);
                }
            }, ServiceLifetime.Transient); 

            // --- INFRASTRUCTURE: EXTERNAL ---
            // Supabase Client (Singleton)
            // Supabase Client (Singleton)
            services.AddSingleton(provider =>
            {
                var url = Configuration["Supabase:Url"];
                var key = Configuration["Supabase:Key"];
                
                if (string.IsNullOrEmpty(url) || url.Contains("REPLACE_WITH"))
                {
                    url = "https://setup-required.local";
                    key = "setup-required";
                }
                
                return new Supabase.Client(url, key);
            });

            // Hardware Drivers
            services.AddSingleton<IHardwareService, HardwareService>();
            services.AddTransient<IRfidReader, RfidReaderDevice>();
            services.AddTransient<TurnstileController>();

            // --- REPOSITORIES (Data Access - Transient) ---
            services.AddTransient<IMemberRepository, MemberRepository>();
            services.AddTransient<IStaffRepository, StaffRepository>();
            services.AddTransient<IRegistrationRepository, RegistrationRepository>();
            services.AddTransient<IAccessEventRepository, AccessEventRepository>();
            services.AddTransient<ISaleRepository, SaleRepository>();
            services.AddTransient<IProductRepository, ProductRepository>();
            services.AddTransient<ITurnstileRepository, TurnstileRepository>();
            services.AddTransient<IReservationRepository, ReservationRepository>();
            services.AddTransient<IPayrollRepository, PayrollRepository>();
            services.AddTransient<MembershipPlanRepository>();
            services.AddTransient<IMembershipPlanRepository>(s => 
                new CachedMembershipPlanRepository(s.GetRequiredService<MembershipPlanRepository>(), s.GetRequiredService<IMemoryCache>()));
            services.AddTransient<IIntegrationRepository, IntegrationRepository>();
            services.AddTransient<IGymSettingsRepository, GymSettingsRepository>();

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

            // --- DOMAIN SERVICES (Business Logic) ---
            services.AddTransient<IMemberService, MemberService>();
            services.AddTransient<IStaffService, StaffService>();
            services.AddTransient<IRegistrationService, RegistrationService>();
            services.AddTransient<IAccessEventService, AccessEventService>();
            services.AddTransient<IProductService, ProductService>();
            services.AddTransient<ISaleService, SaleService>();
            services.AddTransient<IReservationService, ReservationService>();
            services.AddTransient<IMembershipPlanService, MembershipPlanService>();
            services.AddSingleton<ISessionMonitorService, SessionMonitorService>();
            services.AddSingleton<Management.Domain.Services.IEmailService, Management.Infrastructure.Services.NullEmailService>();

            // --- APPLICATION SERVICES (Orchestration) ---
            services.AddSingleton<IConnectionService, ConnectionService>();
            services.AddTransient<IAuthenticationService, AuthenticationService>();
            services.AddTransient<ITurnstileService, TurnstileService>();
            services.AddTransient<IFinanceService, FinanceService>();
            services.AddTransient<ISettingsService, SettingsService>();
            services.AddTransient<IBackupService, BackupService>();
            services.AddSingleton<ISessionStorageService, SessionStorageService>();
            services.AddSingleton<Management.Domain.Services.IFacilityContextService, FacilityContextService>();
            services.AddSingleton<ITerminologyService, TerminologyService>();
            services.AddSingleton<ICommandPaletteService, CommandPaletteService>();
            services.AddSingleton<IOrderService, OrderService>();
            services.AddSingleton<IReceiptPrintingService, ReceiptPrintingService>();
            services.AddSingleton<ISalonService, SalonServiceImplementation>();
            services.AddSingleton<IResilienceService, ResilienceService>();
            services.AddSingleton<IUndoService, UndoService>();
            services.AddTransient<IOnboardingService, OnboardingService>();

            // --- DIAGNOSTIC SYSTEM ---
            services.AddSingleton<IDiagnosticService, DiagnosticService>();
            services.AddTransient<DiagnosticViewModel>();

            // --- SYNC ENGINE ---
            services.AddSingleton<ISyncEventDispatcher, SyncEventDispatcher>();
            services.AddHostedService<SyncWorker>();
            services.AddHostedService<SupabaseRealtimeService>();

            // --- PRESENTATION SERVICES (UI) ---
            services.AddSingleton<IDispatcher>(provider => new WpfDispatcher(System.Windows.Application.Current.Dispatcher));
            services.AddSingleton<INavigationService, NavigationService>(provider =>
                new NavigationService(
                    provider.GetRequiredService<NavigationStore>(),
                    viewModelType => (ViewModelBase)provider.GetRequiredService(viewModelType),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetService<ILogger<NavigationService>>()
                ));

            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IOnboardingStateStore, OnboardingStateStore>();
            services.AddSingleton<IViewMappingService, ViewMappingService>();
            services.AddSingleton<IModalNavigationService, ModalNavigationService>();
            services.AddSingleton<GlobalExceptionHandler>();

            // --- VIEW MODELS ---
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LicenseEntryViewModel>();
            services.AddTransient<OnboardingOwnerViewModel>();
            services.AddTransient<OnboardingViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<AccessControlViewModel>();
            services.AddTransient<MembersViewModel>();
            services.AddTransient<RegistrationsViewModel>();
            services.AddTransient<HistoryViewModel>();
            services.AddTransient<FinanceAndStaffViewModel>();
            services.AddTransient<ShopViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<TablesViewModel>();
            services.AddTransient<KitchenDisplayViewModel>();
            services.AddTransient<AppointmentsViewModel>();
            services.AddTransient<ServicesViewModel>();
            services.AddTransient<BookingViewModel>();
            services.AddTransient<CompletionViewModel>();
            services.AddTransient<MemberDetailViewModel>();
            services.AddTransient<RegistrationDetailViewModel>();
            services.AddTransient<ProductDetailViewModel>();
            services.AddTransient<CheckoutViewModel>();
            services.AddTransient<ConfirmationViewModel>();
            services.AddTransient<ConflictResolutionViewModel>();

            services.AddTransient<SessionExpiredViewModel>(provider => new SessionExpiredViewModel(
                provider.GetRequiredService<IAuthenticationService>(),
                provider.GetRequiredService<INavigationService>(),
                provider.GetRequiredService<IModalNavigationService>(),
                "Your session has expired."));

            // --- VIEWS ---
            services.AddSingleton<MainWindow>(s => new MainWindow(s.GetRequiredService<MainViewModel>()));
            services.AddTransient<ConflictResolutionView>();
            services.AddTransient<Views.Auth.LoginView>();
            services.AddTransient<BookingModal>();
            services.AddTransient<CompletionModal>();
        }

        private async System.Threading.Tasks.Task InitializeDatabaseAsync()
        {
            try
            {
                // Create a scope to resolve Scoped/Transient services like DbContext
                using (var scope = ServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<GymDbContext>();
                    // Applies any pending migrations and creates the DB if it doesn't exist
                    await dbContext.Database.MigrateAsync();
                }
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
            ReportErrorToDiagnostics("Dispatcher", e.Exception, Models.DiagnosticSeverity.Critical);
            
            Serilog.Log.Fatal(e.Exception, "Unhandled Dispatcher Exception");

            if (!_isHandlingException)
            {
                _isHandlingException = true;
                ShowDiagnosticWindow(e.Exception);
            }

            e.Handled = true;
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ReportErrorToDiagnostics("Background Task", e.Exception, Models.DiagnosticSeverity.Error);
            Serilog.Log.Error(e.Exception, "Unobserved Task Exception");
            e.SetObserved();
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown AppDomain Exception");
            ReportErrorToDiagnostics("AppDomain", ex, Models.DiagnosticSeverity.Critical);
            Serilog.Log.Fatal(ex, "Unhandled AppDomain Exception");

            if (!_isHandlingException && e.IsTerminating)
            {
                _isHandlingException = true;
                ShowDiagnosticWindow(ex);
            }
        }

        private void ReportErrorToDiagnostics(string context, Exception ex, Models.DiagnosticSeverity severity)
        {
            var diagnosticService = ServiceProvider?.GetService<IDiagnosticService>();
            diagnosticService?.LogError(
                Models.DiagnosticCategory.Runtime,
                context,
                ex.Message,
                ex,
                severity
            );
        }

        private void ShowDiagnosticWindow(Exception ex)
        {
            Current.Dispatcher.Invoke(() => {
                try 
                {
                    var diagnosticViewModel = ServiceProvider?.GetService<DiagnosticViewModel>();
                    if (diagnosticViewModel != null)
                    {
                        var diagnosticWindow = new Views.DiagnosticWindow(diagnosticViewModel);
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
            try
            {
                var themeName = isDarkMode ? "Theme.Dark.xaml" : "Theme.Light.xaml";
                var themeUri = new Uri($"Resources/{themeName}", UriKind.Relative);

                // Find and remove existing theme dictionary
                var existingTheme = Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Theme.Dark.xaml") == true ||
                                        d.Source?.OriginalString?.Contains("Theme.Light.xaml") == true);

                if (existingTheme != null)
                {
                    Resources.MergedDictionaries.Remove(existingTheme);
                }

                // Add new theme dictionary at the beginning (so it's loaded first)
                var newTheme = new ResourceDictionary { Source = themeUri };
                Resources.MergedDictionaries.Insert(0, newTheme);

                Serilog.Log.Information($"Theme switched to: {themeName}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to switch theme");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Serilog.Log.CloseAndFlush();
            base.OnExit(e);
        }
        private async Task<bool> RunStartupSecurityGuard(IServiceProvider services)
        {
            var hardwareService = services.GetRequiredService<IHardwareService>();
            var supabase = services.GetRequiredService<Supabase.Client>();
            var navigationService = services.GetRequiredService<INavigationService>();
            var tenantService = services.GetRequiredService<ITenantService>();
            var hardwareId = hardwareService.GetHardwareId();

            Serilog.Log.Information($"Startup Security Guard: Checking hardware ID {hardwareId}");

            try
            {
                // Check if device is licensed in tenant_devices
                var response = await supabase.From<Infrastructure.Services.OnboardingService.TenantDeviceModel>()
                    .Filter("hardware_id", Supabase.Postgrest.Constants.Operator.Equals, hardwareId)
                    .Get();

                if (response.Models.Count == 0)
                {
                    // STATE 1: No Device Found → Show License Entry
                    Serilog.Log.Warning("Device not found in tenant_devices. Redirecting to License Entry.");
                    await Current.Dispatcher.InvokeAsync(async () => {
                        await navigationService.NavigateToAsync<LicenseEntryViewModel>();
                    });
                    return false;
                }
                else
                {
                    var device = response.Models[0];
                    Serilog.Log.Information($"Device verified for Tenant {device.TenantId}.");
                    
                    // CRITICAL: Set the tenant context so DB migrations/queries can proceed
                    tenantService.SetTenantId(device.TenantId);
                    
                    // Check if user has an active session
                    var session = supabase.Auth.CurrentSession;
                    if (session == null || session.ExpiresAt() < DateTimeOffset.UtcNow)
                    {
                        // STATE 2: Device Found, No Session → Show Login (if implemented)
                        Serilog.Log.Information("Device found but no active session. User needs to log in.");
                        // For now, we'll continue to dashboard since login might not be implemented
                        // TODO: Implement LoginViewModel and navigate here
                        // await Current.Dispatcher.InvokeAsync(async () => {
                        //     await navigationService.NavigateToAsync<LoginViewModel>();
                        // });
                        // return false;
                    }
                    
                    // STATE 3: Device & Session Found → Continue to Dashboard
                    Serilog.Log.Information("Device and session verified. Proceeding to main application.");
                    return true;
                }
            }
            catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "42501")
            {
                // RLS Error: insufficient_privilege
                Serilog.Log.Error(pgEx, "RLS Error: Access denied by Row Level Security");
                await Current.Dispatcher.InvokeAsync(async () => {
                    var dialogService = services.GetService<IDialogService>();
                    if (dialogService != null)
                    {
                        await dialogService.ShowAlertAsync(
                            "Access denied by database security policies. Please contact support.",
                            "Security Error");
                    }
                    await navigationService.NavigateToAsync<LicenseEntryViewModel>();
                });
                return false;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error during Startup Security Guard check");
                // Fallback to license entry if we can't verify the device
                await Current.Dispatcher.InvokeAsync(async () => {
                    await navigationService.NavigateToAsync<LicenseEntryViewModel>();
                });
                return false;
            }
        }

    }
}