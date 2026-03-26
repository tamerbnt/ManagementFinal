using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private readonly IHardwareService _hardwareService;
        private readonly IBackupService _backupService;
        private readonly Lazy<DeviceManagementViewModel> _deviceManagement;

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

        // Apparatus / Peripherals — [ObservableProperty] allows single-replace instead of per-item Add
        [ObservableProperty]
        private ObservableCollection<DeviceStatusViewModel> _localDevices = new();

        // Drawer Content
        [ObservableProperty]
        private object? _currentDrawerContent;

        // Backup Information
        [ObservableProperty]
        private string _backupFolderPath = string.Empty;

        [ObservableProperty]
        private string _lastBackupDateDisplay = "Never";

        [ObservableProperty]
        private string _lastBackupSizeDisplay = "0 KB";

        [ObservableProperty]
        private CultureInfo? _selectedLanguage;

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
            IFacilityContextService facilityContext,
            SessionManager sessionManager,
            ILocalizationService localizationService,
            IHardwareService hardwareService,
            Management.Presentation.Services.Salon.ISalonService salonService,
            IBackupService backupService,
            ModalNavigationStore modalNavigationStore,
            Lazy<DeviceManagementViewModel> deviceManagement) : base()
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
            
            _modalNavigationStore = modalNavigationStore;

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
            
            // Navigation
            Shortcuts.Add(new ShortcutItem("Ctrl + 1", "Navigate to Home", "Navigation"));
            Shortcuts.Add(new ShortcutItem("Ctrl + 2", "Navigate to Members", "Navigation"));
            Shortcuts.Add(new ShortcutItem("Ctrl + 3", "Navigate to Sales", "Navigation"));
            Shortcuts.Add(new ShortcutItem("Ctrl + 4", "Navigate to Staff", "Navigation"));
            Shortcuts.Add(new ShortcutItem("Ctrl + 6", "Navigate to Settings", "Navigation"));

            // Search & Tools
            Shortcuts.Add(new ShortcutItem("Ctrl + F / Ctrl + K", "Focus Search Bar", "Tools"));
            Shortcuts.Add(new ShortcutItem("Ctrl + P", "Open Command Palette", "Tools"));

            // Quick Actions
            Shortcuts.Add(new ShortcutItem("Ctrl + N", "Quick Create Member", "Actions"));
            Shortcuts.Add(new ShortcutItem("Ctrl + Q", "Quick Sale / Walk-In", "Actions"));

            // General
            Shortcuts.Add(new ShortcutItem("Enter", "Submit Form / Confirm Action", "General"));
            Shortcuts.Add(new ShortcutItem("Escape", "Close Modal / Cancel", "General"));
        }

        public Task PreInitializeAsync()
        {
            Title = _localizationService.GetString("Terminology.Sidebar.Settings");
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task LoadDeferredAsync()
        {
            IsActive = true;
            UpdateUserInfo();
            return Task.CompletedTask;
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
                                    IsSessionPack = dto.IsSessionPack,
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
                                    IsSessionPack = dto.IsSessionPack,
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

            // Phase 4: Dynamic loading (Legacy plans logic)
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

            CurrentDrawerContent = null;
        }

        [RelayCommand]
        public void CloseDrawer() // Changed from private to public for external binding
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
                    IsSessionPack = m.IsSessionPack,
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
                    IsSessionPack = w.IsSessionPack,
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
        }        [RelayCommand]
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

        [ObservableProperty] private bool _isSessionPack;
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

        [ObservableProperty] private bool _isSessionPack;
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
