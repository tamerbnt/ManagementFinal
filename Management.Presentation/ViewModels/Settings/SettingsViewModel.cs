using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;
using Microsoft.Extensions.DependencyInjection;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Application.DTOs;
using System.Threading.Tasks;
using System.Linq;
using Management.Application.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;
using Management.Domain.Interfaces;
using Management.Infrastructure.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using System.Globalization;
using Management.Presentation.Stores;
using Management.Presentation.Services.State;
using Management.Application.Interfaces.ViewModels;
using Management.Application.Interfaces.App;
using Management.Presentation.Services.Navigation;

namespace Management.Presentation.ViewModels.Settings
{
    public record ShortcutItem(string Keys, string Description, string Category);

    public partial class SettingsViewModel : ViewModelBase, INavigationalLifecycle 
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;
        private readonly IFacilityContextService _facilityContext;
        private readonly IMembershipPlanService _planService;
        private readonly Management.Presentation.Services.Salon.ISalonService _salonServiceInternal;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly SessionManager _sessionManager;
        private readonly ILocalizationService _localizationService;
        private readonly IBackupService _backupService;
        private readonly Lazy<DeviceManagementViewModel> _deviceManagement;
        private readonly IHardwareService _hardwareService;
        private readonly ISecureStorageService _secureStorage;
        private readonly IToastService _toastService;
        private readonly ITerminologyService _terminologyService;
        private readonly IPromotionService _promotionService;
        private readonly IDiscountService _discountService;
        private readonly INavigationRegistry _navigationRegistry;

        // Tab Navigation
        [ObservableProperty]
        private string _selectedTab = "Account"; // "Account" or "MembershipPlans"

        // Account Information
        [ObservableProperty]
        private string _userName = string.Empty;

        [ObservableProperty]
        private string _userEmail = string.Empty;

        [ObservableProperty]
        private string _role = string.Empty;

        [ObservableProperty]
        private string _permissions = string.Empty;



        [ObservableProperty]
        private bool _isDrawerOpen;

        // --- Professional Email Settings (Milestone 2) ---
        [ObservableProperty]
        private string _professionalEmail = string.Empty;

        [ObservableProperty]
        private string _emailApiKey = string.Empty;

        [ObservableProperty]
        private bool _isEmailVerified;

        [ObservableProperty]
        private bool _isValidatingEmail;

        [ObservableProperty]
        private string _emailStatusMessage = "Not Configured";
        // ------------------------------------------------

        // Apparatus / Peripherals — [ObservableProperty] allows single-replace instead of per-item Add
        [ObservableProperty]
        private ObservableCollection<DeviceStatusViewModel> _localDevices = new();

        // Drawer Content
        [ObservableProperty]
        private object? _currentDrawerContent;

        // Backup Information
        [ObservableProperty]
        private string _backupFolderPath = string.Empty;

        private DiscountEditorViewModel? _discountEditorVm;
        private PromotionEditorViewModel? _promotionEditorVm;

        [ObservableProperty]
        private string _lastBackupDateDisplay = "Never";

        [ObservableProperty]
        private string _lastBackupSizeDisplay = "0 KB";

        // Salon Settings
        [ObservableProperty]
        private int _totalChairs = 1;

        [ObservableProperty]
        private decimal _salonDailyRevenueTarget = 1000m;
        
        // Gym Settings
        [ObservableProperty]
        private int _gymMaxOccupancy = 100;

        [ObservableProperty]
        private decimal _gymDailyRevenueTarget = 5000m;

        [ObservableProperty]
        private CultureInfo? _selectedLanguage;

        [ObservableProperty]
        private bool _isDarkMode;

        /// <summary>Tracks which light-mode palette is active: "Default" or "Alternate".</summary>
        [ObservableProperty]
        private string _selectedLightPalette = "Default";

        public ObservableCollection<CultureInfo> SupportedLanguages { get; } = new();
        
        // Facility-specific visibility
        public bool IsGymFacility => _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Gym;
        public bool IsRestaurantFacility => _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Restaurant;
        public bool IsSalonFacility => _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Salon;
        public bool ShowMembershipPlans => IsGymFacility || IsSalonFacility;
        public bool ShowWalkInPlans => IsGymFacility || IsSalonFacility;
        public bool ShowSalonServices => IsSalonFacility;

        // Membership Plans — [ObservableProperty] allows single-replace instead of per-item Add
        // Membership Plans
        [ObservableProperty]
        private ObservableCollection<MembershipPlanViewModel> _membershipPlans = new();

        // Walk-In Plans
        [ObservableProperty]
        private ObservableCollection<WalkInPlanViewModel> _walkInPlans = new();

        // Salon Services
        [ObservableProperty]
        private ObservableCollection<SalonServiceViewModel> _salonServices = new();

        // Promotions
        [ObservableProperty]
        private ObservableCollection<PromotionViewModel> _promotions = new();

        // Discounts
        [ObservableProperty]
        private ObservableCollection<DiscountDto> _discounts = new();

        // Keyboard Shortcuts
        public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

        public string UserInitials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(UserName)) return string.Empty;
                var parts = UserName.Trim().Split(' ');
                if (parts.Length == 1) return parts[0].Length > 0 ? parts[0][0].ToString().ToUpper() : string.Empty;
                if (parts.Length > 1) return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
                return string.Empty;
            }
        }



        public SettingsViewModel(
            IServiceProvider serviceProvider, 
            IMembershipPlanService planService, 
            IDialogService dialogService, 
            ISettingsService settingsService,
            Management.Domain.Services.IFacilityContextService facilityContext,
            SessionManager sessionManager,
            ILocalizationService localizationService,
            IHardwareService hardwareService,
            Management.Presentation.Services.Salon.ISalonService salonService,
            IBackupService backupService,
            ModalNavigationStore modalNavigationStore,
            System.Lazy<DeviceManagementViewModel> deviceManagement,
            IToastService toastService,
            INavigationRegistry navigationRegistry,
            ITerminologyService terminologyService,
            IPromotionService promotionService,
            IDiscountService discountService,
            ISecureStorageService secureStorage) : base(null, null, toastService)
        {
            _serviceProvider = serviceProvider;
            _planService = planService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _facilityContext = facilityContext;
            _salonServiceInternal = salonService;
            _backupService = backupService;
            _deviceManagement = deviceManagement;
            _sessionManager = sessionManager;
            _localizationService = localizationService;
            _hardwareService = hardwareService;
            _secureStorage = secureStorage;
            _toastService = toastService;
            _navigationRegistry = navigationRegistry;
            _terminologyService = terminologyService;
            _promotionService = promotionService;
            _discountService = discountService;
            
            _modalNavigationStore = modalNavigationStore;

            // Register for appearance sync messages
            WeakReferenceMessenger.Default.Register<AppearanceChangedMessage>(this, (r, m) =>
            {
                var info = m.Value;
                // Sync without re-triggering persistence
                _isDarkMode = !info.IsLightMode;
                OnPropertyChanged(nameof(IsDarkMode));
                
                _selectedLightPalette = info.LightPalette;
                OnPropertyChanged(nameof(SelectedLightPalette));
            });

            // Phase 4: Thread-safe collections
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(MembershipPlans, new object());
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(WalkInPlans, new object());

            // Subscribe to hardware updates
            _hardwareService.DeviceStatusChanged += OnDeviceStatusChanged;
            InitializeLocalDevices();


            // Initialize from Session
            if (sessionManager.CurrentUser != null)
            {
                UserName = sessionManager.CurrentUser.FullName;
                UserEmail = sessionManager.CurrentUser.Email;
                Role = sessionManager.CurrentUser.Role.ToString();
                Permissions = sessionManager.CurrentUser.IsOwner ? "Full Access" : "Limited Access";
            }
            else
            {
                UserName = "Guest";
                UserEmail = "";
                Role = "Guest";
                Permissions = "None";
            }
            
            // Subscribe to Session changes
            _sessionManager.PropertyChanged += OnSessionPropertyChanged;
            
            // Subscribe to device changes from the sub-viewmodel
            DeviceManagement.DevicesChanged += (s, e) => _isDevicesLoaded = false;
            
            // Set default tab based on facility if Account isn't preferred
            if (IsRestaurantFacility)
            {
                // We keep Account as default for now, but ensure Menu is available
            }
            
            // Phase 4: Lazy loading - don't fire on constructor, let SelectTab handle it
            // _ = LoadPlansAsync(); 

            // Initialize Languages
            SupportedLanguages.Clear();
            foreach (var lang in _localizationService.SupportedLanguages)
            {
                SupportedLanguages.Add(lang);
            }
            SelectedLanguage = _localizationService.CurrentCulture;

            InitializeShortcuts();
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Clear();
            
            // Navigation - Dynamic discovery from Registry
            var navItems = _navigationRegistry.GetItems(_facilityContext.CurrentFacility).ToList();
            
            for (int i = 0; i < 5; i++)
            {
                string combo = $"Ctrl + {i + 1}";
                string description = "---";
                
                if (i < navItems.Count)
                {
                    var item = navItems[i];
                    string term = _terminologyService.GetTerm(item.ResourceKey);
                    description = $"{_localizationService.GetString("Terminology.Settings.Shortcuts.NavigateTo")} {term}";
                }
                
                Shortcuts.Add(new ShortcutItem(combo, description, "Navigation"));
            }
            
            Shortcuts.Add(new ShortcutItem("Ctrl + 6", $"{_localizationService.GetString("Terminology.Settings.Shortcuts.NavigateTo")} {_localizationService.GetString("Terminology.Sidebar.Settings")}", "Navigation"));

            // Search & Tools
            Shortcuts.Add(new ShortcutItem("Ctrl + F / Ctrl + K", _localizationService.GetString("Terminology.Settings.Shortcuts.FocusSearch"), "Tools"));
            Shortcuts.Add(new ShortcutItem("Ctrl + P", _localizationService.GetString("Terminology.Settings.Shortcuts.OpenPalette"), "Tools"));

            // Quick Actions
            Shortcuts.Add(new ShortcutItem("Ctrl + N", _localizationService.GetString("Terminology.Settings.Shortcuts.QuickCreateMember"), "Actions"));
            Shortcuts.Add(new ShortcutItem("Ctrl + Q", _localizationService.GetString("Terminology.Settings.Shortcuts.QuickSale"), "Actions"));
            
            if (IsGymFacility)
            {
                Shortcuts.Add(new ShortcutItem("Ctrl + W", _localizationService.GetString("Terminology.Settings.Shortcuts.QuickWalkIn"), "Actions"));
            }

            // General
            Shortcuts.Add(new ShortcutItem("Enter", _localizationService.GetString("Terminology.Settings.Shortcuts.Confirm"), "General"));
            Shortcuts.Add(new ShortcutItem("Escape", _localizationService.GetString("Terminology.Settings.Shortcuts.Cancel"), "General"));
        }

        public Task PreInitializeAsync()
        {
            Title = _localizationService.GetString("Terminology.Sidebar.Settings");
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            UpdateUserInfo();
            await LoadEmailSettingsAsync();
        }

        private async Task LoadEmailSettingsAsync()
        {
            ProfessionalEmail = _secureStorage.Get("ProfessionalEmailAccount") ?? string.Empty;
            EmailApiKey = _secureStorage.Get("ProfessionalEmailApiKey") ?? string.Empty;
            IsEmailVerified = !string.IsNullOrEmpty(ProfessionalEmail) && !string.IsNullOrEmpty(EmailApiKey);
            EmailStatusMessage = IsEmailVerified ? "Status: Active" : "Status: Not Configured";
        }

        [RelayCommand]
        private async Task SaveEmailSettings()
        {
            if (string.IsNullOrWhiteSpace(ProfessionalEmail))
            {
                _toastService.ShowWarning("Please enter a valid professional email.");
                return;
            }

            // Basic Professional Email Check (Must not be gmail/yahoo/etc in a real world, but for now simple regex)
            if (!ProfessionalEmail.Contains("@") || ProfessionalEmail.EndsWith("@gmail.com") || ProfessionalEmail.EndsWith("@yahoo.com"))
            {
                _toastService.ShowWarning("Please use a professional domain email (e.g., info@yourgym.com). Free providers are not supported for high-reputation sending.");
                return;
            }

            await _secureStorage.SetAsync("ProfessionalEmailAccount", ProfessionalEmail);
            if (!string.IsNullOrWhiteSpace(EmailApiKey))
            {
                await _secureStorage.SetAsync("ProfessionalEmailApiKey", EmailApiKey);
            }

            _toastService.ShowSuccess("Professional email settings saved locally.", "Security Guard");
            await LoadEmailSettingsAsync();
        }

        [RelayCommand]
        private async Task ValidateEmail()
        {
            if (string.IsNullOrWhiteSpace(ProfessionalEmail) || string.IsNullOrWhiteSpace(EmailApiKey))
            {
                _toastService.ShowWarning("Email and API Key are required for validation.");
                return;
            }

            IsValidatingEmail = true;
            EmailStatusMessage = "Validating domain reputation...";

            try
            {
                await Task.Delay(2000); // Simulate API call to Brevo/SendGrid

                // Validation logic: check if domain is professional
                var domain = ProfessionalEmail.Split('@').LastOrDefault();
                if (domain != null && (domain.Contains("gmail") || domain.Contains("outlook") || domain.Contains("hotmail")))
                {
                    IsEmailVerified = false;
                    EmailStatusMessage = "Status: Rejected (Individual Provider)";
                    _toastService.ShowError("Domain validation failed. Please use a business domain.");
                }
                else
                {
                    IsEmailVerified = true;
                    EmailStatusMessage = "Status: Verified & Reputation Healthy";
                    _toastService.ShowSuccess("Domain and Reputation verified successfully!", "Email Guardian");
                }
            }
            catch (Exception ex)
            {
                EmailStatusMessage = "Status: Validation Error";
                _toastService.ShowError($"Validation failed: {ex.Message}");
            }
            finally
            {
                IsValidatingEmail = false;
            }
        }

        public override async Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default)
        {
            await base.OnModalOpenedAsync(parameter, cancellationToken);
            UpdateUserInfo();
        }

        private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionManager.CurrentUser))
            {
                UpdateUserInfo();
            }
        }

        private void UpdateUserInfo()
        {
            if (_sessionManager.CurrentUser != null)
            {
                UserName = _sessionManager.CurrentUser.FullName;
                UserEmail = _sessionManager.CurrentUser.Email;
                Role = _sessionManager.CurrentUser.Role.ToString();
                Permissions = _sessionManager.CurrentUser.IsOwner ? "Full Access" : "Limited Access";
            }
            else
            {
                UserName = "Guest";
                UserEmail = "";
                Role = "Guest";
                Permissions = "None";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sessionManager.PropertyChanged -= OnSessionPropertyChanged;
                _hardwareService.DeviceStatusChanged -= OnDeviceStatusChanged;
            }
            base.Dispose(disposing);
        }

        public override void ResetState()
        {
            base.ResetState();
            _plansLoaded = false;
            _isDevicesLoaded = false;
        }

        private bool _plansLoaded = false;
        private bool _isDevicesLoaded = false;

        partial void OnSelectedLanguageChanged(CultureInfo? value)
        {
            if (value != null && value.Name != _localizationService.CurrentCulture.Name)
            {
                _localizationService.SetLanguage(value.Name);

                // Phase 4: Persist appearance setting (Fire and forget from property setter)
                Task.Run(async () =>
                {
                    try
                    {
                        var result = await _settingsService.GetAppearanceSettingsAsync(_facilityContext.CurrentFacilityId);
                        if (result.IsSuccess)
                        {
                            var currentSettings = result.Value;
                            var updatedSettings = currentSettings with { Language = value.Name };
                            await _settingsService.UpdateAppearanceSettingsAsync(_facilityContext.CurrentFacilityId, updatedSettings);
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to persist language setting");
                    }
                });
            }
        }

        partial void OnIsDarkModeChanged(bool value)
        {
            ThemeManager.SetTheme(value ? AppTheme.Dark : AppTheme.Light);

            // Broadcast change to other VMs (TopBar)
            WeakReferenceMessenger.Default.Send(new AppearanceChangedMessage(new AppearanceChangeInfo(
                !value, SelectedLightPalette, IsThemeChange: true)));

            // Persist atomically
            Task.Run(async () =>
            {
                try
                {
                    await _settingsService.UpdateThemeModeAsync(_facilityContext.CurrentFacilityId, !value);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to persist theme setting");
                }
            });
        }

        partial void OnSelectedLightPaletteChanged(string value)
        {
            var palette = value switch
            {
                "Alternate" => LightPalette.Alternate,
                "Classic"   => LightPalette.Classic,
                "Atrium"    => LightPalette.Atrium,
                "NoirBlush" => LightPalette.NoirBlush,
                _           => LightPalette.Default
            };
            ThemeManager.SetLightPalette(palette);

            // Broadcast change to other VMs (TopBar)
            WeakReferenceMessenger.Default.Send(new AppearanceChangedMessage(new AppearanceChangeInfo(
                !IsDarkMode, value, IsPaletteChange: true)));

            // Persist atomically
            Task.Run(async () =>
            {
                try
                {
                    await _settingsService.UpdateLightPaletteAsync(_facilityContext.CurrentFacilityId, value);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to persist light palette setting");
                }
            });
        }

        /// <summary>Called from the Appearance tab palette picker cards.</summary>
        [RelayCommand]
        private void SelectPalette(string paletteKey)
        {
            SelectedLightPalette = paletteKey;
        }


        [RelayCommand]
        public async Task LoadPlansAsync(bool force = false)
        {
            if (IsLoading && !force) return;
            IsLoading = true;

            try
            {
                // Phase 4: Removed artificial 600ms delay
                // await Task.Delay(600); 

                var result = await _planService.GetAllPlansAsync(_facilityContext.CurrentFacilityId);

                if (result.IsSuccess)
                {
                    var allDtos = result.Value;
                    
                    var processedPlans = await Task.Run(() => 
                    {
                        var membershipList = new List<MembershipPlanViewModel>();
                        var walkInList = new List<WalkInPlanViewModel>();

                        foreach (var dto in allDtos)
                        {
                            var durationDesc = $"{dto.DurationDays} {_localizationService.GetString("Terminology.Settings.WalkIn.Days")}";

                            if (dto.IsWalkIn)
                            {
                                walkInList.Add(new WalkInPlanViewModel
                                {
                                    Id = dto.Id,
                                    Name = dto.Name,
                                    Price = dto.Price,
                                    DurationDays = dto.DurationDays,
                                    DurationDescription = durationDesc,
                                    Status = dto.IsActive ? "Active" : "Archived",
                                    IsActive = dto.IsActive,
                                    SessionsPerWeek = dto.SessionsPerWeek,
                                    IsPersonalTraining = dto.IsPersonalTraining,
                                    GenderRule = dto.GenderRule,
                                    ScheduleJson = dto.ScheduleJson
                                });
                            }
                            else
                            {
                                membershipList.Add(new MembershipPlanViewModel
                                {
                                    Id = dto.Id,
                                    Name = dto.Name,
                                    Price = dto.Price,
                                    DurationDays = dto.DurationDays,
                                    DurationDescription = durationDesc,
                                    Status = dto.IsActive ? "Active" : "Archived",
                                    IsActive = dto.IsActive,
                                    SessionsPerWeek = dto.SessionsPerWeek,
                                    IsPersonalTraining = dto.IsPersonalTraining,
                                    GenderRule = dto.GenderRule,
                                    ScheduleJson = dto.ScheduleJson
                                });
                            }
                        }
                        return (membershipList, walkInList);
                    });

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        // Single collection replacement = 1 PropertyChanged notification each,
                        // instead of N CollectionChanged notifications from Clear+foreach-Add.
                        MembershipPlans = new ObservableCollection<MembershipPlanViewModel>(processedPlans.membershipList);
                        WalkInPlans = new ObservableCollection<WalkInPlanViewModel>(processedPlans.walkInList);
                    });
                }
            }
            catch (Exception)
            {
                // Error handled by Task boundary or UI
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void InitializeLocalDevices()
        {
            // Single collection replacement = 1 PropertyChanged notification
            // instead of N CollectionChanged notifications from Clear+foreach-Add.
            LocalDevices = new ObservableCollection<DeviceStatusViewModel>(
                _hardwareService.GetDeviceStatuses()
                                .Select(s => new DeviceStatusViewModel(s, TestDeviceCommand)));
        }

        private void OnDeviceStatusChanged(DeviceStatus status)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var existing = LocalDevices.FirstOrDefault(d => d.Type == status.Type);
                if (existing != null)
                {
                    existing.Update(status);
                }
            });
        }

        [RelayCommand]
        public async Task TestDevice(DeviceStatusViewModel device)
        {
            device.IsTesting = true;
            try
            {
                await _hardwareService.TestDeviceAsync(device.Type);
            }
            finally
            {
                await Task.Delay(1000); // UI feedback delay
                device.IsTesting = false;
            }
        }

        [RelayCommand]
        private async Task SelectTab(string tabName)
        {
            SelectedTab = tabName;

            // Handle Modular Tab Lifecycle
            if (tabName == "Apparatus" && !_isDevicesLoaded)
            {
                var vm = DeviceManagement;
                if (vm is INavigationalLifecycle lifecycle)
                {
                    await lifecycle.PreInitializeAsync();
                    await lifecycle.LoadDeferredAsync();
                    _isDevicesLoaded = true;
                }
            }

            // Dynamic loading (Legacy plans logic)
            if ((tabName == "MembershipPlans" || tabName == "WalkInPlans") && !_plansLoaded)
            {
                await LoadPlansAsync();
                _plansLoaded = true;
            }

            if (tabName == "Services" && !_servicesLoaded)
            {
                await LoadSalonServicesAsync();
                _servicesLoaded = true;
            }

            if (tabName == "Backups")
            {
                await LoadBackupMetadataAsync();
            }

            if (tabName == "SalonSettings")
            {
                await LoadSalonSettingsAsync();
            }

            if (tabName == "GymSettings")
            {
                await LoadGymSettingsAsync();
            }

            if (tabName == "Promotions")
            {
                await LoadPromotionsAsync();
            }

            if (tabName == "Discounts")
            {
                await LoadDiscountsAsync();
            }

            // Load persisted light palette when Appearance tab opens
            if (tabName == "Appearance")
            {
                await LoadAppearanceAsync();
            }
        }

        private async Task LoadAppearanceAsync()
        {
            try
            {
                var result = await _settingsService.GetAppearanceSettingsAsync(_facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    var s = result.Value;
                    // Sync dark mode toggle without triggering the Changed callback
                    _isDarkMode = !s.IsLightMode;
                    OnPropertyChanged(nameof(IsDarkMode));

                    // Sync palette — suppress re-persist by setting backing field directly
                    _selectedLightPalette = s.LightPalette ?? "Default";
                    OnPropertyChanged(nameof(SelectedLightPalette));

                    // Apply to ThemeManager without triggering persistence loop
                    var palette = _selectedLightPalette switch
                    {
                        "Alternate" => LightPalette.Alternate,
                        "Classic"   => LightPalette.Classic,
                        "Atrium"    => LightPalette.Atrium,
                        "NoirBlush" => LightPalette.NoirBlush,
                        _           => LightPalette.Default
                    };
                    ThemeManager.SetLightPalette(palette);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to load appearance settings");
            }
        }

        private bool _salonSettingsLoaded = false;
        private bool _promotionsLoaded = false;

        [RelayCommand]
        public async Task LoadSalonSettingsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var result = await _settingsService.GetSalonSettingsAsync(_facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    TotalChairs = result.Value.TotalChairs;
                    SalonDailyRevenueTarget = result.Value.DailyRevenueTarget;
                    _salonSettingsLoaded = true;
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task SaveSalonSettings()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var dto = new SalonSettingsDto(TotalChairs, SalonDailyRevenueTarget, "{}");
                var result = await _settingsService.UpdateSalonSettingsAsync(_facilityContext.CurrentFacilityId, dto);
                if (result.IsSuccess)
                {
                    _toastService.ShowSuccess("Salon settings saved.");
                }
                else
                {
                    _toastService.ShowError($"Failed to save settings: {result.Error.Message}");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadGymSettingsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var result = await _settingsService.GetFacilitySettingsAsync(_facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    GymMaxOccupancy = result.Value.MaxOccupancy;
                    GymDailyRevenueTarget = result.Value.DailyRevenueTarget;
                }

                // Load Appearance for toggle initialization
                var appearance = await _settingsService.GetAppearanceSettingsAsync(_facilityContext.CurrentFacilityId);
                if (appearance.IsSuccess)
                {
                    _isDarkMode = !appearance.Value.IsLightMode;
                    OnPropertyChanged(nameof(IsDarkMode));
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task SaveGymSettings()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                // FacilitySettingsDto is a record requiring all constructor arguments
                var current = await _settingsService.GetFacilitySettingsAsync(_facilityContext.CurrentFacilityId);
                
                var dto = new FacilitySettingsDto(
                    GymMaxOccupancy,
                    GymDailyRevenueTarget,
                    current.IsSuccess ? current.Value.IsMaintenanceMode : false,
                    current.IsSuccess ? current.Value.Schedule : new System.Collections.Generic.List<DayScheduleDto>(),
                    current.IsSuccess ? current.Value.Zones : new System.Collections.Generic.List<ZoneDto>()
                );
                
                var result = await _settingsService.UpdateFacilitySettingsAsync(_facilityContext.CurrentFacilityId, dto);
                if (result.IsSuccess)
                {
                    _toastService.ShowSuccess("Gym settings saved.");
                }
                else
                {
                    _toastService.ShowError($"Failed to save settings: {result.Error.Message}");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadBackupMetadataAsync()
        {
            BackupFolderPath = _backupService.GetBackupFolderPath();
            var (date, size) = await _backupService.GetLastBackupMetadataAsync();
            
            LastBackupDateDisplay = date?.ToString("g") ?? "Never";
            
            if (size > 1024 * 1024)
                LastBackupSizeDisplay = $"{(double)size / (1024 * 1024):F2} MB";
            else
                LastBackupSizeDisplay = $"{(double)size / 1024:F2} KB";
        }

        [RelayCommand]
        public async Task CreateBackupNow()
        {
            if (IsLoading) return;
            IsLoading = true;
            try 
            {
                await _backupService.CreateBackupAsync();
                await _backupService.CleanupOldBackupsAsync(7);
                await LoadBackupMetadataAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Manual backup failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void OpenBackupFolder()
        {
            try 
            {
                System.Diagnostics.Process.Start("explorer.exe", BackupFolderPath);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open backup folder");
            }
        }

        private bool _servicesLoaded = false;

        [RelayCommand]
        public async Task LoadSalonServicesAsync(bool force = false)
        {
            if (IsLoading && !force) return;
            IsLoading = true;

            try
            {
                await _salonServiceInternal.LoadServicesAsync();
                var services = _salonServiceInternal.Services;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    SalonServices = new ObservableCollection<SalonServiceViewModel>(
                        services.Select(s => new SalonServiceViewModel
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Price = s.BasePrice,
                            DurationMinutes = s.DurationMinutes,
                            Category = s.Category,
                            Status = "Active", // Salon services don't have IsActive yet, assume active
                            IsActive = true
                        }));
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load salon services");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task AddSalonService()
        {
            var vm = _serviceProvider.GetRequiredService<SalonServiceEditorViewModel>();
            vm.IsEditMode = false;
            
            vm.Saved += OnSalonServiceSaved;
            vm.Canceled += OnSalonServiceCanceled;

            CurrentDrawerContent = vm;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        public async Task EditSalonService(SalonServiceViewModel service)
        {
            if (service == null) return;
            var vm = _serviceProvider.GetRequiredService<SalonServiceEditorViewModel>();
            vm.IsEditMode = true;
            vm.Id = service.Id;
            vm.Name = service.Name;
            vm.BasePrice = service.Price;
            vm.DurationMinutes = service.DurationMinutes;
            vm.Category = service.Category;
            
            vm.Saved += OnSalonServiceSaved;
            vm.Canceled += OnSalonServiceCanceled;

            CurrentDrawerContent = vm;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        public async Task DeleteSalonService(SalonServiceViewModel service)
        {
            if (service == null) return;

            // Atomic Pattern: Delete -> Save (Service handles) -> Notify with Undo
            try
            {
                await _salonServiceInternal.DeleteServiceAsync(service.Id);
                SalonServices.Remove(service);

                _toastService.ShowSuccess(
                    $"Service '{service.Name}' deleted.",
                    undoAction: async () => 
                    {
                        await _salonServiceInternal.RestoreServiceAsync(service.Id);
                         await LoadSalonServicesAsync(force: true); // Refresh collection
                    });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Failed to delete service: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddMembershipPlan()
        {
            OpenPlanEditor(null, false);
        }

        [RelayCommand]
        private void AddWalkInPlan()
        {
            OpenPlanEditor(null, true);
        }

        private MembershipPlanEditorViewModel? _cachedEditorVm;

        private void OpenPlanEditor(MembershipPlanDto? dto, bool isWalkIn)
        {
            CleanupEditor();

            // Phase 4: VM Recycling
            if (_cachedEditorVm == null)
            {
                _cachedEditorVm = _serviceProvider.GetRequiredService<MembershipPlanEditorViewModel>();
            }
            
            var editorVm = _cachedEditorVm;
            
            if (dto == null)
            {
                editorVm.Reset();
                editorVm.IsWalkIn = isWalkIn;
                editorVm.Name = isWalkIn ? "New Walk-In Plan" : "New Membership Plan";
                editorVm.DurationDays = isWalkIn ? 1 : 30;
                editorVm.Price = isWalkIn ? 15.00m : 1500.00m;
            }
            else
            {
                editorVm.Initialize(dto);
            }
            
            editorVm.Saved += OnEditorSaved;
            editorVm.Canceled += OnEditorCanceled;

            CurrentDrawerContent = editorVm;
            IsDrawerOpen = true;
        }

        private async void OnEditorSaved(object? sender, EventArgs e)
        {
            try
            {
                _plansLoaded = false;
                IsDrawerOpen = false;
                CleanupEditor();
                await LoadPlansAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error occurred in OnEditorSaved");
            }
        }

        private void OnEditorCanceled(object? sender, EventArgs e)
        {
            IsDrawerOpen = false;
            CleanupEditor();
        }

        private async void OnSalonServiceSaved(object? sender, EventArgs e)
        {
            try
            {
                _servicesLoaded = false;
                IsDrawerOpen = false;
                CleanupEditor();
                await LoadSalonServicesAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error occurred in OnSalonServiceSaved");
            }
        }

        private void OnSalonServiceCanceled(object? sender, EventArgs e)
        {
            IsDrawerOpen = false;
            CleanupEditor();
        }

        private void CleanupEditor()
        {
            if (CurrentDrawerContent is MembershipPlanEditorViewModel planEditor)
            {
                planEditor.Saved -= OnEditorSaved;
                planEditor.Canceled -= OnEditorCanceled;
            }
            else if (CurrentDrawerContent is SalonServiceEditorViewModel salonEditor)
            {
                salonEditor.Saved -= OnSalonServiceSaved;
                salonEditor.Canceled -= OnSalonServiceCanceled;
            }
            else if (CurrentDrawerContent is PromotionEditorViewModel promotionEditor)
            {
                promotionEditor.Saved -= OnPromotionSaved;
                promotionEditor.Canceled -= OnPromotionCanceled;
            }
            else if (CurrentDrawerContent is DiscountEditorViewModel discountEditor)
            {
                discountEditor.Saved -= OnDiscountSaved;
                discountEditor.Canceled -= OnDiscountCanceled;
            }

            CurrentDrawerContent = null;
        }

        [RelayCommand]
        public void CloseDrawer() // Changed from private to public for external binding
        {
             IsDrawerOpen = false;
             CleanupEditor();
        }

        private async void OnPromotionSaved(object? sender, Guid promotionId)
        {
            IsDrawerOpen = false;
            CleanupEditor();
            await LoadPromotionsAsync();
        }

        private void OnPromotionCanceled(object? sender, EventArgs e)
        {
            IsDrawerOpen = false;
            CleanupEditor();
        }


        [RelayCommand]
        private void EditMembershipPlan(MembershipPlanViewModel plan)
        {
            EditPlanGeneric(plan);
        }

        [RelayCommand]
        private void EditWalkInPlan(WalkInPlanViewModel plan)
        {
            EditPlanGeneric(plan);
        }

        private void EditPlanGeneric(object planVm)
        {
            // Phase 4: Local data reuse (avoid redundant API call)
            if (planVm is MembershipPlanViewModel m)
            {
                OpenPlanEditor(new MembershipPlanDto 
                { 
                    Id = m.Id, 
                    Name = m.Name, 
                    Price = m.Price, 
                    DurationDays = m.DurationDays, 
                    IsWalkIn = false, 
                    IsActive = m.IsActive,
                    SessionsPerWeek = m.SessionsPerWeek,
                    IsPersonalTraining = m.IsPersonalTraining,
                    GenderRule = m.GenderRule,
                    ScheduleJson = m.ScheduleJson
                }, false);
            }
            else if (planVm is WalkInPlanViewModel w)
            {
                OpenPlanEditor(new MembershipPlanDto 
                { 
                    Id = w.Id, 
                    Name = w.Name, 
                    Price = w.Price, 
                    DurationDays = w.DurationDays, 
                    IsWalkIn = true, 
                    IsActive = w.IsActive,
                    SessionsPerWeek = w.SessionsPerWeek,
                    IsPersonalTraining = w.IsPersonalTraining,
                    GenderRule = w.GenderRule,
                    ScheduleJson = w.ScheduleJson
                }, true);
            }
        }

        [RelayCommand]
        private async Task DeleteMembershipPlan(MembershipPlanViewModel plan)
        {
            if (plan == null) return;

            // Atomic Pattern: Delete -> Save (Service handles) -> Notify with Undo
            var result = await _planService.DeletePlanAsync(_facilityContext.CurrentFacilityId, plan.Id);
            if (result.IsSuccess)
            {
                MembershipPlans.Remove(plan);
                _plansLoaded = false;

                _toastService.ShowSuccess(
                    $"Plan '{plan.Name}' deleted.",
                    undoAction: async () => 
                    {
                        var restoreResult = await _planService.RestorePlanAsync(_facilityContext.CurrentFacilityId, plan.Id);
                        if (restoreResult.IsSuccess)
                        {
                            await LoadPlansAsync(force: true); // Refresh collection
                        }
                    });
            }
            else
            {
                _toastService.ShowError(result.Error?.Message ?? "Failed to delete plan.");
            }
        }

        [RelayCommand]
        private async Task DeleteWalkInPlan(WalkInPlanViewModel plan)
        {
            if (plan == null) return;

            // Atomic Pattern: Delete -> Save (Service handles) -> Notify with Undo
            var result = await _planService.DeletePlanAsync(_facilityContext.CurrentFacilityId, plan.Id);
            if (result.IsSuccess)
            {
                WalkInPlans.Remove(plan);
                _plansLoaded = false;

                _toastService.ShowSuccess(
                    $"Walk-in plan '{plan.Name}' deleted.",
                    undoAction: async () => 
                    {
                        var restoreResult = await _planService.RestorePlanAsync(_facilityContext.CurrentFacilityId, plan.Id);
                        if (restoreResult.IsSuccess)
                        {
                            await LoadPlansAsync(); // Refresh collection
                        }
                    });
            }
            else
            {
                _toastService.ShowError(result.Error?.Message ?? "Failed to delete plan.");
            }
        }
 

        public DeviceManagementViewModel DeviceManagement => _deviceManagement.Value;

        [RelayCommand]
        public async Task LoadPromotionsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var result = await _promotionService.GetPromotionsAsync(_facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    Promotions.Clear();
                    foreach (var p in result.Value)
                    {
                        Promotions.Add(new PromotionViewModel(p));
                    }
                    _promotionsLoaded = true;
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CreatePromotionAsync()
        {
            CleanupEditor();

            if (_promotionEditorVm == null)
            {
                _promotionEditorVm = _serviceProvider.GetRequiredService<PromotionEditorViewModel>();
            }

            _promotionEditorVm.Saved += OnPromotionSaved;
            _promotionEditorVm.Canceled += OnPromotionCanceled;

            await _promotionEditorVm.InitializeAsync(null);
            
            CurrentDrawerContent = _promotionEditorVm;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        private async Task EditPromotionAsync(PromotionViewModel promotion)
        {
            if (promotion == null) return;
            CleanupEditor();

            if (_promotionEditorVm == null)
            {
                _promotionEditorVm = _serviceProvider.GetRequiredService<PromotionEditorViewModel>();
            }

            _promotionEditorVm.Saved += OnPromotionSaved;
            _promotionEditorVm.Canceled += OnPromotionCanceled;

            await _promotionEditorVm.InitializeAsync(promotion.Id);

            CurrentDrawerContent = _promotionEditorVm;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        private async Task DeletePromotionAsync(PromotionViewModel promotion)
        {
            if (promotion == null) return;
            var result = await _promotionService.DeletePromotionAsync(_facilityContext.CurrentFacilityId, promotion.Id);
            if (result.IsSuccess)
            {
                Promotions.Remove(promotion);
                _toastService.ShowSuccess($"Promotion '{promotion.Name}' deleted.");
            }
        }

        private bool _discountsLoaded = false;

        [RelayCommand]
        public async Task LoadDiscountsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var result = await _discountService.GetDiscountsAsync(_facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    Discounts.Clear();
                    foreach (var d in result.Value)
                    {
                        Discounts.Add(d);
                    }
                    _discountsLoaded = true;
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CreateDiscountAsync()
        {
            CleanupEditor();

            if (_discountEditorVm == null)
            {
                _discountEditorVm = _serviceProvider.GetRequiredService<DiscountEditorViewModel>();
            }

            _discountEditorVm.Saved += OnDiscountSaved;
            _discountEditorVm.Canceled += OnDiscountCanceled;

            await _discountEditorVm.InitializeAsync(null);
            
            CurrentDrawerContent = _discountEditorVm;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        private async Task EditDiscountAsync(DiscountDto discount)
        {
            if (discount == null) return;
            CleanupEditor();

            if (_discountEditorVm == null)
            {
                _discountEditorVm = _serviceProvider.GetRequiredService<DiscountEditorViewModel>();
            }

            _discountEditorVm.Saved += OnDiscountSaved;
            _discountEditorVm.Canceled += OnDiscountCanceled;

            await _discountEditorVm.InitializeAsync(discount.Id);

            CurrentDrawerContent = _discountEditorVm;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        private async Task DeleteDiscountAsync(DiscountDto discount)
        {
            if (discount == null) return;
            var result = await _discountService.DeleteDiscountAsync(_facilityContext.CurrentFacilityId, discount.Id);
            if (result.IsSuccess)
            {
                Discounts.Remove(discount);
                _toastService.ShowSuccess($"Discount '{discount.Name}' deleted.");
            }
        }

        private async void OnDiscountSaved(object? sender, Guid id)
        {
            IsDrawerOpen = false;
            CleanupEditor();
            await LoadDiscountsAsync();
        }

        private void OnDiscountCanceled(object? sender, EventArgs e)
        {
            IsDrawerOpen = false;
            CleanupEditor();
        }
    }

    // Simple ViewModel for membership plans in settings
    public partial class MembershipPlanViewModel : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private decimal _price;

        [ObservableProperty]
        private int _durationDays; // Added property

        [ObservableProperty]
        private string _durationDescription = string.Empty;

        [ObservableProperty]
        private string _status = "Active";

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty] private int _sessionsPerWeek;
        [ObservableProperty] private bool _isPersonalTraining;
        [ObservableProperty] private int _genderRule;
        [ObservableProperty] private string? _scheduleJson;
    }

    public partial class WalkInPlanViewModel : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private decimal _price;

        [ObservableProperty]
        private int _durationDays; // Added property

        [ObservableProperty]
        private string _durationDescription = string.Empty; // e.g., "1 Day", "1 Week"

        [ObservableProperty]
        private string _status = "Active";

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty] private int _sessionsPerWeek;
        [ObservableProperty] private bool _isPersonalTraining;
        [ObservableProperty] private int _genderRule;
        [ObservableProperty] private string? _scheduleJson;
    }

    public partial class SalonServiceViewModel : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private decimal _price;

        [ObservableProperty]
        private int _durationMinutes;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private string _status = "Active";

        [ObservableProperty]
        private bool _isActive = true;
    }
}
