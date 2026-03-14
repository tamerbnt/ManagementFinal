using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Services;
using Management.Presentation.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;
using Management.Domain.Services;
using Management.Infrastructure.Services;
using Management.Domain.Primitives;
using Management.Presentation.ViewModels.Base;
using Management.Application.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces.ViewModels;

namespace Management.Presentation.ViewModels
{
    public partial class EmailConfirmationViewModel : FacilityAwareViewModelBase, IParameterReceiver
    {
        private readonly INavigationService _navigationService;
        private readonly IOnboardingService _onboardingService;


        [ObservableProperty]
        private string _email = string.Empty;

        public EmailConfirmationViewModel(
            INavigationService navigationService,
            IOnboardingService onboardingService,
            IToastService toastService,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            ILogger<EmailConfirmationViewModel> logger,
            IDiagnosticService diagnosticService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _navigationService = navigationService;
            _onboardingService = onboardingService;
            
            Title = _localizationService?.GetString("Strings.Auth.Title.VerifyEmail") ?? "Verify Email";
        }

        public Task SetParameterAsync(object parameter)
        {
            if (parameter is string email)
            {
                Email = email;
            }
            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            if (IsLoading) return;

            await ExecuteLoadingAsync(async () =>
            {
                var result = await _onboardingService.CheckVerificationStatusAsync(Email);
                
                if (result.IsSuccess && result.Value)
                {
                    Serilog.Log.Information($"[EmailConfirmationViewModel] Verification confirmed for {Email}. Navigating to Login.");
                    await _navigationService.NavigateToAsync<LoginViewModel>();
                }
                else
                {
                    Serilog.Log.Warning($"[EmailConfirmationViewModel] Verification check failed or pending for {Email}.");
                    _toastService?.ShowWarning(_localizationService?.GetString("Strings.Auth.Toast.VerificationPending") ?? "Verification Pending", _localizationService?.GetString("Strings.Auth.Message.VerificationPending") ?? "We couldn't confirm your activation yet. Please click the link in your email and try again.");
                }
            }, _localizationService?.GetString("Strings.Auth.Loading.CheckingVerification") ?? "Checking verification status...");
        }

        [RelayCommand]
        private async Task ResendEmailAsync()
        {
            if (IsLoading) return;
            
            await ExecuteLoadingAsync(async () =>
            {
                var result = await _onboardingService.ResendConfirmationEmailAsync(Email);
                
                if (result.IsSuccess)
                {
                    _toastService?.ShowSuccess(_localizationService?.GetString("Strings.Auth.Toast.VerificationEmailSent") ?? "Verification Email Sent", string.Format(_localizationService?.GetString("Strings.Auth.Message.VerificationEmailSent") ?? "A new link has been sent to {0}", Email));
                }
                else
                {
                    _toastService?.ShowError(_localizationService?.GetString("Strings.Auth.Toast.ResendFailed") ?? "Resend Failed", result.Error.Message);
                }
            }, _localizationService?.GetString("Strings.Auth.Loading.ResendingVerification") ?? "Resending verification email...");
        }
    }
}
