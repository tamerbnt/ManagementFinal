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
    public partial class BusinessInfoViewModel : WizardStepViewModel
    {
        private readonly OnboardingState _state;

        [ObservableProperty]
        private string _businessName = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private string _selectedCurrency = string.Empty;

        public List<string> Currencies { get; } = new() { "USD ($)", "EUR (€)", "GBP (£)", "JPY (¥)", "CAD ($)", "AUD ($)" };

        public BusinessInfoViewModel(
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
            Title = _localizationService?.GetString("Strings.Onboarding.BusinessProfile") ?? "Business Profile";
            
            // Sync initial state
            BusinessName = _state.BusinessName;
            Phone = _state.Phone;
            Address = _state.Address;
            SelectedCurrency = string.IsNullOrEmpty(_state.SelectedCurrency) ? "USD ($)" : _state.SelectedCurrency;
        }

        protected override void OnLanguageChanged()
        {
            Title = _localizationService?.GetString("Strings.Onboarding.BusinessProfile") ?? "Business Profile";
        }

        public override bool Validate()
        {
            ErrorMessage = string.Empty;
            HasError = false;

            if (string.IsNullOrWhiteSpace(BusinessName))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.BusinessNameRequired") ?? "Please enter your business name.";
                HasError = true;
                return false;
            }

            if (string.IsNullOrWhiteSpace(Phone))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.PhoneNumberRequired") ?? "Please enter a phone number.";
                HasError = true;
                return false;
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.AddressRequired") ?? "Please enter your business address.";
                HasError = true;
                return false;
            }

            _state.BusinessName = BusinessName;
            _state.Phone = Phone;
            _state.Address = Address;
            _state.SelectedCurrency = SelectedCurrency;
            
            IsValid = true;
            return true;
        }
    }
}
