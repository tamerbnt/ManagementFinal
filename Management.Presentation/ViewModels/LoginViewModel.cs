using System;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using System.Windows.Controls;
using Management.Application.Services;
using System.Windows.Input;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Services;
using Management.Application.Services;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Infrastructure.Services;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly IAuthenticationService _authService;
        private readonly INavigationService _navigationService;
        private readonly ISessionStorageService _sessionStorage;
        private readonly IDialogService _dialogService;
        private readonly IOnboardingStateStore _onboardingState;
        private readonly IOnboardingService _onboardingService;

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string? ExpansionMessage => _onboardingState.ExpansionMessage;
        public bool IsExpansionMode => !string.IsNullOrEmpty(ExpansionMessage);

        private bool _rememberMe;
        public bool RememberMe
        {
            get => _rememberMe;
            set => SetProperty(ref _rememberMe, value);
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                SetProperty(ref _errorMessage, value);
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand ForgotPasswordCommand { get; }

        public LoginViewModel(
            IAuthenticationService authService,
            INavigationService navigationService,
            ISessionStorageService sessionStorage,
            IDialogService dialogService,
            IOnboardingStateStore onboardingState,
            IOnboardingService onboardingService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _sessionStorage = sessionStorage;
            _dialogService = dialogService;
            _onboardingState = onboardingState;
            _onboardingService = onboardingService;

            LoginCommand = new RelayCommand<object>(ExecuteLogin, CanExecuteLogin);
            ForgotPasswordCommand = new RelayCommand(ExecuteForgotPassword);
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !IsBusy && !string.IsNullOrWhiteSpace(Email);
        }

        private async void ExecuteLogin(object? parameter)
        {
            if (parameter is not PasswordBox passwordBox) return;

            var password = passwordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Please enter your password.";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // Clear any existing session first
                await _sessionStorage.ClearSessionAsync();

                var result = await _authService.LoginAsync(Email, password);

                if (result.IsSuccess)
                {
                    // Schema 3: Expansion Flow Logic
                    if (IsExpansionMode && _onboardingState.TargetTenantId.HasValue)
                    {
                        var tenantId = _onboardingState.TargetTenantId.Value;
                        
                        // 1. Check Device Count (The 3-PC Gate)
                        var count = await _onboardingService.GetDeviceCountAsync(tenantId);
                        if (count >= 3)
                        {
                            await _authService.LogoutAsync(); // Clean up
                            ErrorMessage = "Device Limit Reached. This workspace is already active on 3 machines.";
                            await _dialogService.ShowAlertAsync("Registration Failed", ErrorMessage);
                            return;
                        }

                        // 2. Register this device
                        var regResult = await _onboardingService.RegisterCurrentDeviceAsync(tenantId, $"{Environment.MachineName} (Expansion)");
                        if (regResult.IsFailure)
                        {
                            await _authService.LogoutAsync();
                            ErrorMessage = $"Failed to link this device: {regResult.Error.Message}";
                            return;
                        }

                        // Clear expansion state after success
                        _onboardingState.Clear();
                    }

                    // Navigate to Main Dashboard (Index 0)
                    await _navigationService.NavigateToAsync(0);
                }
                else
                {
                    ErrorMessage = result.Error.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteForgotPassword()
        {
            // TODO: Implement Forgot Password Flow (Modal or Link)
            // TODO: Implement Forgot Password Flow (Modal or Link)
             _dialogService.ShowAlertAsync("Forgot Password", "Please contact your administrator to reset your password.");
        }
    }
}
