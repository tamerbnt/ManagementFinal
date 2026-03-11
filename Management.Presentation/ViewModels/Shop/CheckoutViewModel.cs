using System;
using Management.Presentation.Extensions;
using Microsoft.Extensions.Logging;
using Management.Presentation.ViewModels.Base;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Domain.Services;
using Management.Presentation.Services.Localization;

namespace Management.Presentation.ViewModels.Shop
{
    public class CheckoutViewModel : FacilityAwareViewModelBase
    {
        public CheckoutViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<CheckoutViewModel> logger, 
            IDiagnosticService diagnosticService, 
            IToastService toastService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            Title = GetTerm("Strings.Shop.Checkout") ?? "Checkout";
        }

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Strings.Shop.Checkout") ?? "Checkout";
        }
    }
}
