using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces.App;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Management.Domain.Models;
using Management.Domain.Enums;
using Management.Domain.Services;
using MediatR;
using Management.Application.Notifications;

namespace Management.Presentation.ViewModels.GymHome
{
    public partial class RegisterWalkInViewModel : ViewModelBase
    {
        private readonly IGymOperationService _gymService;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly IFacilityContextService _facilityContext;
        private readonly IMediator _mediator;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLeadCommand))]
        private string _fullName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLeadCommand))]
        private string _phoneNumber = string.Empty;


        private readonly ITerminologyService _terminologyService;
        private readonly Services.Localization.ILocalizationService _localizationService;

        public RegisterWalkInViewModel(
            IGymOperationService gymService,
            ModalNavigationStore modalNavigationStore,
            IFacilityContextService facilityContext,
            IMediator mediator,
            ITerminologyService terminologyService,
            Services.Localization.ILocalizationService localizationService)
        {
            _gymService = gymService;
            _modalNavigationStore = modalNavigationStore;
            _facilityContext = facilityContext;
            _mediator = mediator;
            _terminologyService = terminologyService;
            _localizationService = localizationService;

            _localizationService.LanguageChanged += (s, e) => UpdateTitle();
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            Title = System.Windows.Application.Current?.TryFindResource("Terminology.GymHome.WalkIn.Title") as string 
                    ?? "Process Walk-In";
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveLeadAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                var result = await _gymService.RegisterLeadAsync(
                    FullName,
                    PhoneNumber,
                    _facilityContext.CurrentFacilityId);

                if (result.Success)
                {
                    // Publish notification
                    await _mediator.Publish(new FacilityActionCompletedNotification(
                        _facilityContext.CurrentFacilityId,
                        "Registration",
                        FullName,
                        $"New Walk-In Lead registered: {FullName} ({PhoneNumber})",
                        result.MemberId.ToString()));

                    await _modalNavigationStore.CloseAsync(ModalResult.Success(result));
                }
            }, "Failed to register lead.");
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(FullName) && !string.IsNullOrWhiteSpace(PhoneNumber);
        }
    }
}
