using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using AsyncRelayCommand = CommunityToolkit.Mvvm.Input.AsyncRelayCommand;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Domain.Services;
using Management.Infrastructure.Services;
using Management.Application.Services;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels
{
    public class FacilityTypeOption
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public string GradientStart { get; set; } = "#0EA5E9";
        public string GradientEnd { get; set; } = "#2563EB";
    }

    public class FacilityOnboardingViewModel : FacilityAwareViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly ITenantService _tenantService;
        private readonly IOnboardingService _onboardingService;

        public IAsyncRelayCommand ContinueCommand { get; }
        public IAsyncRelayCommand NavigateBackCommand { get; }

        private FacilityTypeOption? _selectedOption;
        public FacilityTypeOption? SelectedOption
        {
            get => _selectedOption;
            set 
            {
                if (SetProperty(ref _selectedOption, value))
                {
                    ((AsyncRelayCommand)ContinueCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _isBusyLocal;
        public new bool IsBusy
        {
            get => _isBusyLocal;
            set 
            {
                if (SetProperty(ref _isBusyLocal, value))
                {
                    ((AsyncRelayCommand)ContinueCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<FacilityTypeOption> _options = new();
        public ObservableCollection<FacilityTypeOption> Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }

        public FacilityOnboardingViewModel(
            INavigationService navigationService,
            IDialogService dialogService,
            IOnboardingService onboardingService,
            ITenantService tenantService,
            IFacilityContextService facilityContextService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            ILogger<FacilityOnboardingViewModel> logger,
            IDiagnosticService diagnosticService)
            : base(terminologyService, facilityContextService, logger, diagnosticService, null, localizationService)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _onboardingService = onboardingService;
            _tenantService = tenantService;

            ContinueCommand = new AsyncRelayCommand(ExecuteContinueAsync, () => SelectedOption != null && !IsBusy);
            NavigateBackCommand = new AsyncRelayCommand(ExecuteNavigateBackAsync);
            
            InitializeOptions();

            // Default selection
            if (Options.Count > 0)
                SelectedOption = Options[0];
        }

        private void InitializeOptions()
        {
            var options = new List<FacilityTypeOption>
            {
                new FacilityTypeOption 
                { 
                    Name = "Gym", 
                    Description = _localizationService?.GetString("Strings.Auth.Facility.GymDesc") ?? "Fitness centers, CrossFit boxes, and Personal Training studios.",
                    IconKey = "Icon.Dumbbell",
                    GradientStart = "#0EA5E9",
                    GradientEnd = "#2563EB"
                },
                new FacilityTypeOption 
                { 
                    Name = "Salon", 
                    Description = _localizationService?.GetString("Strings.Auth.Facility.SalonDesc") ?? "Hair salons, Spas, and Wellness centers.",
                    IconKey = "Icon.Sparkles",
                    GradientStart = "#EC4899",
                    GradientEnd = "#D946EF"
                },
                new FacilityTypeOption 
                { 
                    Name = "Restaurant", 
                    Description = _localizationService?.GetString("Strings.Auth.Facility.RestaurantDesc") ?? "Cafes, Fine dining, and Quick service restaurants.",
                    IconKey = "Icon.Utensils",
                    GradientStart = "#F59E0B",
                    GradientEnd = "#EF4444"
                }
            };
            Options = new ObservableCollection<FacilityTypeOption>(options);
        }

        protected override void OnLanguageChanged()
        {
            InitializeOptions();
        }

        private async Task ExecuteContinueAsync()
        {
            if (SelectedOption == null) return;

            IsBusy = true;
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null)
                {
                    await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.RegistrationError") ?? "Registration Error", _localizationService?.GetString("Strings.Auth.Message.SessionExpired") ?? "Session expired or tenant not found. Please restart the process.");
                    return;
                }

                // 1. Update cloud industry preference
                var updateResult = await _onboardingService.UpdateTenantIndustryAsync(tenantId.Value, SelectedOption.Name);
                if (updateResult.IsFailure)
                {
                    Serilog.Log.Error($"[FacilityOnboarding] Failed to update industry: {updateResult.Error.Message}");
                }
                
                // 2. Discover Facility IDs from cloud to populate local context
                Serilog.Log.Information("[FacilityOnboarding] Discovering provisioned facilities...");
                var discoveryResult = await _onboardingService.GetLicensedFacilitiesAsync();
                
                if (discoveryResult.IsSuccess && discoveryResult.Value.Count > 0)
                {
                    var mappings = new Dictionary<Management.Domain.Enums.FacilityType, Guid>();
                    foreach (var f in discoveryResult.Value)
                    {
                        var type = (Management.Domain.Enums.FacilityType)f.Type;
                        mappings[type] = f.Id;
                    }
                    
                    _facilityContext.UpdateFacilities(mappings);
                    Serilog.Log.Information("[FacilityOnboarding] Local facility IDs updated from discovery.");
                }

                // 3. Set and persist the current facility for this terminal
                if (Enum.TryParse<Management.Domain.Enums.FacilityType>(SelectedOption.Name, out var selectedType))
                {
                    _facilityContext.SwitchFacility(selectedType);
                    Serilog.Log.Information($"[FacilityOnboarding] Terminal configured as primary: {selectedType}");
                }

                await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.SetupComplete") ?? "Setup Complete", string.Format(_localizationService?.GetString("Strings.Auth.Message.SetupComplete") ?? "Facility configured as {0}!", SelectedOption.Name), isSuccess: true);
                
                await Task.Delay(500);
                await _navigationService.NavigateToLoginAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync(_localizationService?.GetString("Strings.Auth.Title.SystemError") ?? "System Error", string.Format(_localizationService?.GetString("Strings.Auth.Message.SetupFailed") ?? "Setup failed: {0}", ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteNavigateBackAsync()
        {
            await _navigationService.NavigateToAsync<OnboardingOwnerViewModel>();
        }
    }
}
