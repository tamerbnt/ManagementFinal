using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Application.Services;
using Management.Domain.Services;
using Microsoft.Extensions.Logging;
using Management.Application.ViewModels.Base;

namespace Management.Presentation.ViewModels.Onboarding.Base
{
    public abstract partial class WizardStepViewModel : FacilityAwareViewModelBase
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isValid;

        protected readonly IDialogService _dialogService;

        protected WizardStepViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger logger,
            IDiagnosticService diagnosticService,
            ILocalizationService localizationService,
            IDialogService dialogService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService)
        {
            _dialogService = dialogService;
        }

        public virtual Task OnEnterAsync() => Task.CompletedTask;
        public virtual Task OnLeaveAsync() => Task.CompletedTask;
        public abstract bool Validate();
    }
}
