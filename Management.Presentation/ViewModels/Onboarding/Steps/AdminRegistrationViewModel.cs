using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Services;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Domain.Services;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces;
using Management.Application.ViewModels.Base;

namespace Management.Presentation.ViewModels.Onboarding.Steps
{
    public partial class AdminRegistrationViewModel : FacilityAwareViewModelBase
    {
        private readonly Management.Application.DTOs.OnboardingState _state;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        /// <summary>
        /// Callback to signal parent ViewModel to move to next step
        /// </summary>
        public Action? RequestNextStep { get; set; }

        public AdminRegistrationViewModel(
            Management.Application.DTOs.OnboardingState state,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger logger,
            IDiagnosticService diagnosticService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService)
        {
            _state = state;
            
            // Hydrate from state if returning
            FullName = _state.AdminFullName;
            Email = _state.AdminEmail;
            Password = _state.AdminPassword;
        }

        [RelayCommand]
        private void CopyPassword()
        {
            if (!string.IsNullOrEmpty(Password))
            {
                System.Windows.Clipboard.SetText(Password);
            }
        }

        [RelayCommand]
        private void CreateAccount()
        {
            // Clear previous errors
            ErrorMessage = string.Empty;
            HasError = false;

            // 1. Validation Logic
            if (string.IsNullOrWhiteSpace(FullName))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.FullNameRequired") ?? "Full name is required.";
                HasError = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.EmailRequired") ?? "Email address is required.";
                HasError = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.PasswordRequired") ?? "Password is required.";
                HasError = true;
                return;
            }

            if (Password.Length < 6)
            {
                ErrorMessage = _localizationService?.GetString("Strings.Auth.Error.PasswordTooShort") ?? "Password must be at least 6 characters long.";
                HasError = true;
                return;
            }

            // 2. Save to State (Deferred Execution)
            _state.AdminFullName = FullName;
            _state.AdminEmail = Email;
            _state.AdminPassword = Password;

            // 3. Move Next
            RequestNextStep?.Invoke();
        }
    }
}
