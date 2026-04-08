using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Management.Domain.Services;

namespace Management.Presentation.ViewModels.Shell
{
    public partial class FacilityAuthViewModel : ViewModelBase
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly Management.Application.Services.IAuthenticationService _authService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly ITerminologyService _terminologyService;
        private readonly Management.Presentation.Services.Localization.ILocalizationService _localizationService;

        private System.Guid _targetFacilityId = System.Guid.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        public new string Title => _terminologyService.GetTerm("Strings.Shell.AuthorizeAccess");

        public FacilityAuthViewModel(
            ModalNavigationStore modalNavigationStore, 
            Management.Application.Services.IAuthenticationService authService,
            Management.Domain.Services.IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            Management.Presentation.Services.Localization.ILocalizationService localizationService)
        {
            _modalNavigationStore = modalNavigationStore;
            _authService = authService;
            _facilityContext = facilityContext;
            _terminologyService = terminologyService;
            _localizationService = localizationService;
            base.Title = Title;
        }

        /// <summary>
        /// Sets the specific facility that the user is trying to switch to.
        /// Authentication will be validated against this target.
        /// </summary>
        public void SetTargetFacility(System.Guid facilityId)
        {
            _targetFacilityId = facilityId;
            Serilog.Log.Information("[FacilityAuth] Target facility set to: {Id}", _targetFacilityId);
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }

        [RelayCommand(CanExecute = nameof(CanSubit))]
        private async Task SubmitAsync()
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = _terminologyService.GetTerm("Strings.Auth.Error.RequiredFields");
                return;
            }

            await ExecuteLoadingAsync(async () =>
            {
                // FIX: Authenticate with the TARGET facility ID, not the current one.
                // If _targetFacilityId is Empty, fallback to current (for same-facility locking cases).
                var facilityIdToValidate = _targetFacilityId != System.Guid.Empty 
                    ? _targetFacilityId 
                    : _facilityContext.CurrentFacilityId;

                var result = await _authService.LoginAsync(Email, Password, facilityIdToValidate);
                
                if (result.IsSuccess)
                {
                    await _modalNavigationStore.CloseAsync(ModalResult.Success());
                }
                else
                {
                    ErrorMessage = result.Error.Message;
                }
            }, _localizationService?.GetString("Strings.Auth.Error.AuthenticationFailed") ?? "Authentication failed.");
        }

        private bool CanSubit() => !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Password);

        partial void OnEmailChanged(string value) => SubmitCommand.NotifyCanExecuteChanged();
        partial void OnPasswordChanged(string value) => SubmitCommand.NotifyCanExecuteChanged();
    }
}
