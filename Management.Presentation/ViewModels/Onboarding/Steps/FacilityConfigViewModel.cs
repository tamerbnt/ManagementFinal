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
        private string _facilityType = "Membership & Session";

        public List<string> FacilityTypes { get; } = new() 
        { 
            "Membership & Session", 
            "Appointment & Service", 
            "POS & Inventory" 
        };

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
            {
                FacilityType = _state.FacilityType switch
                {
                    "Gym" or "membership_session" => "Membership & Session",
                    "Salon" or "appointment_service" => "Appointment & Service",
                    "Restaurant" or "pos_inventory" => "POS & Inventory",
                    _ => _state.FacilityType
                };
            }
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
            _state.Category = Management.Infrastructure.Services.OnboardingService.CategoryToSlug(FacilityType);
            
            IsValid = true;
            return true;
        }

        public async Task<bool> ValidateAsync()
        {
            if (!Validate()) return false;

            if (_dialogService != null)
            {
                string catDesc = FacilityType switch
                {
                    "Membership & Session" => "Membership & Session (Gyms, Fitness Studios, Martial Arts)",
                    "Appointment & Service" => "Appointment & Service (Salons, Spas, Clinics)",
                    "POS & Inventory" => "POS & Inventory (Retail, Supermarkets, Restaurants)",
                    _ => FacilityType
                };

                bool confirmed = await _dialogService.ShowConfirmationAsync(
                    "Confirm Business Category",
                    $"Your business workspace will be permanently structured around:\n\n• {catDesc}\n\nAre you sure you want to lock in this category?",
                    "Confirm & Proceed",
                    "Change Selection");

                if (!confirmed) return false;
            }

            return true;
        }
    }
}
