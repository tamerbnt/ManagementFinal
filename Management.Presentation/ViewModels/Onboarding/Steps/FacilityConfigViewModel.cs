using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Onboarding.Base;
using Microsoft.Extensions.Logging;
using Management.Application.ViewModels.Base;

namespace Management.Presentation.ViewModels.Onboarding.Steps
{
    public partial class FacilityConfigViewModel : WizardStepViewModel
    {
        private readonly OnboardingState _state;

        [ObservableProperty]
        private string _facilityName = string.Empty;

        [ObservableProperty]
        private string _facilityType = "Gym";

        public List<string> FacilityTypes { get; } = new() { "Gym", "Salon", "Restaurant" };

        public FacilityConfigViewModel(
            OnboardingState state,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger logger,
            IDiagnosticService diagnosticService,
            ILocalizationService localizationService,
            IDialogService dialogService)
            : base(terminologyService, facilityContext, logger, diagnosticService, localizationService, dialogService)
        {
            _state = state;
            Title = _localizationService?.GetString("Strings.Auth.Title.FacilityConfiguration") ?? "Facility Configuration";
            
            // Hydrate
            FacilityName = _state.FacilityName;
            if (!string.IsNullOrEmpty(_state.FacilityType))
                FacilityType = _state.FacilityType;
        }

        protected override void OnLanguageChanged()
        {
            Title = _localizationService?.GetString("Strings.Auth.Title.FacilityConfiguration") ?? "Facility Configuration";
        }

        public override bool Validate()
        {
            ErrorMessage = string.Empty;
            HasError = false;

            if (string.IsNullOrWhiteSpace(FacilityName))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.FacilityNameRequired") ?? "Facility name is required.";
                HasError = true;
                return false;
            }

            _state.FacilityName = FacilityName;
            _state.FacilityType = FacilityType;
            
            IsValid = true;
            return true;
        }
    }
}
