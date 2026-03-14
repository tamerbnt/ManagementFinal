using System;
using System.Windows.Input;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels
{
    public class SessionExpiredViewModel : FacilityAwareViewModelBase
    {
        private readonly IAuthenticationService _authService;
        private readonly INavigationService _navigationService;
        private readonly IModalNavigationService _modalService;
        
        // This is a dialog, so we communicate result via Close/Action
        public string Message => _localizationService?.GetString("Strings.Auth.SessionExpired") ?? "Your session has expired.";

        public ICommand LoginCommand { get; }
        public ICommand CloseCommand { get; } // Fallback, effectively Logout

        public SessionExpiredViewModel(
            IAuthenticationService authService,
            INavigationService navigationService,
            IModalNavigationService modalService,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            IDialogService dialogService,
            ILocalizationService localizationService,
            ILogger<SessionExpiredViewModel> logger,
            IDiagnosticService diagnosticService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService, dialogService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _modalService = modalService;

            LoginCommand = new AsyncRelayCommand(ExecuteLogin);
            CloseCommand = new AsyncRelayCommand(ExecuteLogout);
        }

        private async Task ExecuteLogin()
        {
            // Close modal first
            await _modalService.CloseCurrentModalAsync();
            
            // Navigate to Login
            await _navigationService.NavigateToLoginAsync();
        }

        private async Task ExecuteLogout()
        {
            await _modalService.CloseCurrentModalAsync();
            await _authService.LogoutAsync();
            await _navigationService.NavigateToLoginAsync();
        }
    }
}
