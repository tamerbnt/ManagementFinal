using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IMembershipPlanService _planService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;

        // --- 1. ACCORDION STATE (Deep Linking) ---

        private bool _isGeneralExpanded = true;
        public bool IsGeneralExpanded { get => _isGeneralExpanded; set => SetProperty(ref _isGeneralExpanded, value); }

        private bool _isPlansExpanded;
        public bool IsPlansExpanded { get => _isPlansExpanded; set => SetProperty(ref _isPlansExpanded, value); }

        private bool _isFacilityExpanded;
        public bool IsFacilityExpanded { get => _isFacilityExpanded; set => SetProperty(ref _isFacilityExpanded, value); }

        private bool _isAppearanceExpanded;
        public bool IsAppearanceExpanded { get => _isAppearanceExpanded; set => SetProperty(ref _isAppearanceExpanded, value); }

        private bool _isIntegrationsExpanded;
        public bool IsIntegrationsExpanded { get => _isIntegrationsExpanded; set => SetProperty(ref _isIntegrationsExpanded, value); }

        // --- 2. GENERAL SETTINGS STATE ---

        private string _gymName;
        public string GymName { get => _gymName; set => SetProperty(ref _gymName, value); }

        private string _email;
        public string Email { get => _email; set => SetProperty(ref _email, value); }

        private string _phoneNumber;
        public string PhoneNumber { get => _phoneNumber; set => SetProperty(ref _phoneNumber, value); }

        private string _website;
        public string Website { get => _website; set => SetProperty(ref _website, value); }

        private string _taxId;
        public string TaxId { get => _taxId; set => SetProperty(ref _taxId, value); }

        private string _address;
        public string Address { get => _address; set => SetProperty(ref _address, value); }

        private string _logoImage;
        public string LogoImage { get => _logoImage; set => SetProperty(ref _logoImage, value); }

        private bool _isSaving;
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        public ICommand SaveGeneralSettingsCommand { get; }
        public ICommand BrowseLogoCommand { get; }
        public ICommand RemoveLogoCommand { get; }

        // --- 3. MEMBERSHIP PLANS STATE ---

        public ObservableCollection<MembershipPlanItemViewModel> Plans { get; }
            = new ObservableCollection<MembershipPlanItemViewModel>();

        public ICommand CreatePlanCommand { get; }

        // --- 4. FACILITY STATE ---

        private int _maxOccupancy;
        public int MaxOccupancy { get => _maxOccupancy; set => SetProperty(ref _maxOccupancy, value); }

        private bool _isMaintenanceMode;
        public bool IsMaintenanceMode { get => _isMaintenanceMode; set => SetProperty(ref _isMaintenanceMode, value); }

        public ObservableCollection<DayScheduleViewModel> OperatingHours { get; }
            = new ObservableCollection<DayScheduleViewModel>();

        public ObservableCollection<ZoneItemViewModel> Zones { get; }
            = new ObservableCollection<ZoneItemViewModel>();

        public IEnumerable<string> TimeSlots { get; } = GenerateTimeSlots();

        public ICommand SaveFacilitySettingsCommand { get; }
        public ICommand AddZoneCommand { get; }
        public ICommand RemoveZoneCommand { get; }

        // --- 5. APPEARANCE STATE ---

        private bool _isLightMode = true;
        public bool IsLightMode { get => _isLightMode; set => SetProperty(ref _isLightMode, value); }

        private string _selectedLanguage = "English (US)";
        public string SelectedLanguage { get => _selectedLanguage; set => SetProperty(ref _selectedLanguage, value); }

        private string _dateFormat = "MM/DD/YYYY";
        public string DateFormat { get => _dateFormat; set => SetProperty(ref _dateFormat, value); }

        private bool _highContrastEnabled;
        public bool HighContrastEnabled { get => _highContrastEnabled; set => SetProperty(ref _highContrastEnabled, value); }

        private bool _reducedMotionEnabled;
        public bool ReducedMotionEnabled { get => _reducedMotionEnabled; set => SetProperty(ref _reducedMotionEnabled, value); }

        private string _textScale = "100%";
        public string TextScale { get => _textScale; set => SetProperty(ref _textScale, value); }

        public ICommand SaveAppearanceCommand { get; }

        // --- 6. INTEGRATIONS STATE ---

        public ObservableCollection<IntegrationItemViewModel> Integrations { get; }
            = new ObservableCollection<IntegrationItemViewModel>();

        // --- CONSTRUCTOR ---

        public SettingsViewModel(
            ISettingsService settingsService,
            IMembershipPlanService planService,
            IDialogService dialogService,
            INotificationService notificationService)
        {
            _settingsService = settingsService;
            _planService = planService;
            _dialogService = dialogService;
            _notificationService = notificationService;

            // Initialize Commands
            SaveGeneralSettingsCommand = new AsyncRelayCommand(ExecuteSaveGeneralAsync);
            BrowseLogoCommand = new RelayCommand(ExecuteBrowseLogo);
            RemoveLogoCommand = new RelayCommand(() => LogoImage = null);

            CreatePlanCommand = new RelayCommand(ExecuteCreatePlan);

            SaveFacilitySettingsCommand = new AsyncRelayCommand(ExecuteSaveFacilityAsync);
            AddZoneCommand = new RelayCommand(ExecuteAddZone);
            RemoveZoneCommand = new RelayCommand<ZoneItemViewModel>(ExecuteRemoveZone);

            SaveAppearanceCommand = new AsyncRelayCommand(ExecuteSaveAppearanceAsync);

            _ = LoadSettingsAsync();
        }

        // --- DATA LOADING ---

        private async Task LoadSettingsAsync()
        {
            try
            {
                var t1 = LoadGeneralAsync();
                var t2 = LoadFacilityAsync();
                var t3 = LoadPlansAsync();
                var t4 = LoadIntegrationsAsync();
                var t5 = LoadAppearanceAsync();

                await Task.WhenAll(t1, t2, t3, t4, t5);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to load settings. Please restart the application.");
            }
        }

        private async Task LoadGeneralAsync()
        {
            var settings = await _settingsService.GetGeneralSettingsAsync();
            GymName = settings.GymName;
            Email = settings.Email;
            PhoneNumber = settings.PhoneNumber;
            Website = settings.Website;
            TaxId = settings.TaxId;
            Address = settings.Address;
            LogoImage = settings.LogoUrl;
        }

        private async Task LoadFacilityAsync()
        {
            var facility = await _settingsService.GetFacilitySettingsAsync();
            MaxOccupancy = facility.MaxOccupancy;
            IsMaintenanceMode = facility.IsMaintenanceMode;

            OperatingHours.Clear();
            foreach (var day in facility.Schedule)
            {
                OperatingHours.Add(new DayScheduleViewModel(day));
            }

            Zones.Clear();
            foreach (var zone in facility.Zones)
            {
                Zones.Add(new ZoneItemViewModel { Name = zone.Name, Capacity = zone.Capacity });
            }
        }

        private async Task LoadPlansAsync()
        {
            var plans = await _planService.GetAllPlansAsync();
            Plans.Clear();
            foreach (var dto in plans)
            {
                var vm = new MembershipPlanItemViewModel(dto);
                vm.EditPlanCommand = new RelayCommand(() => ExecuteEditPlan(vm.Id));
                Plans.Add(vm);
            }
        }

        private async Task LoadIntegrationsAsync()
        {
            var integrations = await _settingsService.GetIntegrationsAsync();
            Integrations.Clear();
            foreach (var dto in integrations)
            {
                var vm = new IntegrationItemViewModel(dto);
                vm.ConfigureCommand = new RelayCommand(() => ExecuteConfigureIntegration(vm.Name));
                Integrations.Add(vm);
            }
        }

        private async Task LoadAppearanceAsync()
        {
            var app = await _settingsService.GetAppearanceSettingsAsync();
            IsLightMode = app.IsLightMode;
            SelectedLanguage = app.Language;
            DateFormat = app.DateFormat;
            HighContrastEnabled = app.HighContrast;
            ReducedMotionEnabled = app.ReducedMotion;
            TextScale = app.TextScale;
        }

        // --- ACTIONS ---

        private async Task ExecuteSaveGeneralAsync()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(GymName))
            {
                _notificationService.ShowError("Gym Name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Email))
            {
                _notificationService.ShowError("Email Address is required.");
                return;
            }

            IsSaving = true;
            try
            {
                await _settingsService.UpdateGeneralSettingsAsync(new GeneralSettingsDto
                {
                    GymName = GymName,
                    Email = Email,
                    PhoneNumber = PhoneNumber,
                    Address = Address,
                    LogoUrl = LogoImage
                });
                _notificationService.ShowSuccess("General settings saved successfully.");
            }
            catch (Exception)
            {
                _notificationService.ShowError("Failed to save general settings.");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void ExecuteBrowseLogo()
        {
            // Open File Dialog Logic
        }

        private void ExecuteCreatePlan() { /* _dialogService.Show<PlanDetailViewModel>(); */ }
        private void ExecuteEditPlan(Guid id) { /* _dialogService.Show<PlanDetailViewModel>(id); */ }

        private async Task ExecuteSaveFacilityAsync()
        {
            if (MaxOccupancy <= 0)
            {
                _notificationService.ShowError("Max Occupancy must be greater than zero.");
                return;
            }

            try
            {
                await _settingsService.UpdateFacilitySettingsAsync(new FacilitySettingsDto
                {
                    MaxOccupancy = MaxOccupancy,
                    IsMaintenanceMode = IsMaintenanceMode,
                    // Serialize collections back to DTOs
                });
                _notificationService.ShowSuccess("Facility settings saved.");
            }
            catch (Exception)
            {
                _notificationService.ShowError("Failed to save facility settings.");
            }
        }

        private void ExecuteAddZone()
        {
            Zones.Add(new ZoneItemViewModel { Name = "New Zone", Capacity = 10 });
        }

        private void ExecuteRemoveZone(ZoneItemViewModel zone)
        {
            if (zone != null) Zones.Remove(zone);
        }

        private async Task ExecuteSaveAppearanceAsync()
        {
            try
            {
                // Save logic
                _notificationService.ShowSuccess("Appearance settings saved.");
            }
            catch (Exception)
            {
                _notificationService.ShowError("Failed to save appearance settings.");
            }
        }

        private void ExecuteConfigureIntegration(string name)
        {
            // Open specific config modal
        }

        // --- HELPERS ---

        private static IEnumerable<string> GenerateTimeSlots()
        {
            // In a real app, this might generate based on culture (12h vs 24h)
            var times = new List<string>();
            for (int i = 0; i < 24; i++)
            {
                times.Add($"{i:00}:00");
                times.Add($"{i:00}:30");
            }
            return times;
        }
    }

    // --- SUB-VIEWMODELS ---

    public class DayScheduleViewModel : ViewModelBase
    {
        public string DayName { get; }

        private string _openTime;
        public string OpenTime { get => _openTime; set => SetProperty(ref _openTime, value); }

        private string _closeTime;
        public string CloseTime { get => _closeTime; set => SetProperty(ref _closeTime, value); }

        private bool _isOpen;
        public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }

        public DayScheduleViewModel(DayScheduleDto dto)
        {
            DayName = dto.Day;
            OpenTime = dto.Open;
            CloseTime = dto.Close;
            IsOpen = dto.IsActive;
        }
    }

    public class ZoneItemViewModel : ViewModelBase
    {
        private string _name;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private int _capacity;
        public int Capacity { get => _capacity; set => SetProperty(ref _capacity, value); }
    }

    // FIX: Inherit from ViewModelBase to support live updates
    public class MembershipPlanItemViewModel : ViewModelBase
    {
        public Guid Id { get; }
        public string Name { get; }
        public decimal Price { get; }
        public string Status { get; }
        public string DurationDescription { get; }

        public ICommand EditPlanCommand { get; set; }

        public MembershipPlanItemViewModel(MembershipPlanDto dto)
        {
            Id = dto.Id;
            Name = dto.Name;
            Price = dto.Price;
            Status = dto.IsActive ? "Active" : "Archived";
            DurationDescription = $"{dto.DurationMonths} Months Access";
        }
    }

    // FIX: Inherit from ViewModelBase
    public class IntegrationItemViewModel : ViewModelBase
    {
        public string Name { get; }
        public string Description { get; }
        public string IconKey { get; }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        public ICommand ConfigureCommand { get; set; }

        public IntegrationItemViewModel(IntegrationDto dto)
        {
            Name = dto.Name;
            Description = dto.Description;
            IconKey = dto.IconKey;
            IsConnected = dto.IsConnected;
        }
    }
}