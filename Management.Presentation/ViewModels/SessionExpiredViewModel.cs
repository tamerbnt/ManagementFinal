using System;
using Management.Application.Services;
using System.Windows.Input;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Services;
using Management.Application.Services;
using Management.Presentation.Extensions;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class SessionExpiredViewModel : ViewModelBase
    {
        private readonly IAuthenticationService _authService;
        private readonly INavigationService _navigationService;
        private readonly IModalNavigationService _modalService;
        
        // This is a dialog, so we communicate result via Close/Action
        public string Message { get; }

        public ICommand LoginCommand { get; }
        public ICommand CloseCommand { get; } // Fallback, effectively Logout

        public SessionExpiredViewModel(
            IAuthenticationService authService,
            INavigationService navigationService,
            IModalNavigationService modalService,
            string message)
        {
            _authService = authService;
            _navigationService = navigationService;
            _modalService = modalService;
            Message = message;

            LoginCommand = new RelayCommand(ExecuteLogin);
            CloseCommand = new RelayCommand(ExecuteLogout);
        }

        private async void ExecuteLogin()
        {
            // Close modal first
            await _modalService.CloseCurrentModalAsync();
            
            // Navigate to Login
            await _navigationService.NavigateToLoginAsync();
        }

        private async void ExecuteLogout()
        {
            await _modalService.CloseCurrentModalAsync();
            await _authService.LogoutAsync();
            await _navigationService.NavigateToLoginAsync();
        }
    }
}
