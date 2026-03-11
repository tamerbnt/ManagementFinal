using System;
using Microsoft.Extensions.Logging;
using Management.Application.Services;
using Management.Application.ViewModels.Base;
using Management.Presentation.Extensions;
using Management.Presentation.Services.Localization;
using Management.Application.Interfaces.App;
using Management.Domain.Services;

namespace Management.Presentation.ViewModels.Base
{
    public abstract partial class FacilityAwareViewModelBase : ViewModelBase
    {
        protected readonly ITerminologyService _terminologyService;
        protected readonly IFacilityContextService _facilityContext;
        protected readonly ILocalizationService? _localizationService;
        protected readonly IToastService? _toastService;
        protected readonly IDialogService? _dialogService;

        protected FacilityAwareViewModelBase(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger? logger = null,
            IDiagnosticService? diagnosticService = null,
            IToastService? toastService = null,
            ILocalizationService? localizationService = null,
            IDialogService? dialogService = null)
            : base(logger, diagnosticService, toastService)
        {
            _terminologyService = terminologyService ?? throw new ArgumentNullException(nameof(terminologyService));
            _facilityContext = facilityContext ?? throw new ArgumentNullException(nameof(facilityContext));
            _localizationService = localizationService;
            _toastService = toastService;
            _dialogService = dialogService;

            if (_localizationService != null)
                _localizationService.LanguageChanged += OnLanguageChangedInternal;
        }

        private void OnLanguageChangedInternal(object? sender, EventArgs e)
        {
            OnLanguageChanged();
        }

        /// <summary>
        /// Override in subclasses to refresh ViewModel-bound string properties on language change.
        /// </summary>
        protected virtual void OnLanguageChanged() { }

        /// <summary>
        /// Retrieves a facility-aware term based on the current context.
        /// </summary>
        protected string GetTerm(string key)
        {
            return _terminologyService.GetTerm(key);
        }

        /// <summary>
        /// Gets the current facility type from the context.
        /// </summary>
        protected Management.Domain.Enums.FacilityType CurrentFacility => _facilityContext.CurrentFacility;

        /// <summary>
        /// Gets the current facility ID from the context.
        /// </summary>
        protected Guid CurrentFacilityId => _facilityContext.CurrentFacilityId;
    }
}
