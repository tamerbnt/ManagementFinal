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
        private readonly Management.Presentation.Services.IModalNavigationService _modalNavigationService;
        private readonly IFacilityContextService _facilityContext;
        private readonly IMediator _mediator;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLeadCommand))]
        private string _fullName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLeadCommand))]
        private string _phoneNumber = string.Empty;

        public new string Title => "Register Walk-In Lead";

        public RegisterWalkInViewModel(
            IGymOperationService gymService,
            Management.Presentation.Services.IModalNavigationService modalNavigationService,
            IFacilityContextService facilityContext,
            IMediator mediator)
        {
            _gymService = gymService;
            _modalNavigationService = modalNavigationService;
            _facilityContext = facilityContext;
            _mediator = mediator;
            base.Title = "Register Walk-In Lead";
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

                    await _modalNavigationService.CloseCurrentModalAsync();
                }
            }, "Failed to register lead.");
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationService.CloseCurrentModalAsync();
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(FullName) && !string.IsNullOrWhiteSpace(PhoneNumber);
        }
    }
}
