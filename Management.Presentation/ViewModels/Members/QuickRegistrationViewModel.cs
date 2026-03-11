using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Application.Notifications;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Stores;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Members
{
    public partial class QuickRegistrationViewModel : ViewModelBase
    {
        private readonly IMemberService _memberService;
        private readonly IMembershipPlanService _planService;
        private readonly IFacilityContextService _facilityContext;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly IMediator _mediator;
        private readonly IHardwareTurnstileService _turnstileService;
        private readonly IGymOperationService _gymOperationService;
        private readonly Management.Presentation.Services.Salon.ISalonService _salonService;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _cardId = string.Empty;

        [ObservableProperty]
        private MembershipPlanDto? _selectedPlan;

        [ObservableProperty]
        private ObservableCollection<MembershipPlanDto> _plans = new();

        [ObservableProperty]
        private Gender _gender = Gender.Male; // Default to Male

        public ObservableCollection<Gender> GenderOptions { get; } = new() { Gender.Male, Gender.Female };

        public QuickRegistrationViewModel(
            ILogger<QuickRegistrationViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IMemberService memberService,
            IMembershipPlanService planService,
            IFacilityContextService facilityContext,
            ModalNavigationStore modalNavigationStore,
            IMediator mediator,
            IHardwareTurnstileService turnstileService,
            IGymOperationService gymOperationService,
            Management.Presentation.Services.Salon.ISalonService salonService)
            : base(logger, diagnosticService, toastService)
        {
            _memberService = memberService;
            _planService = planService;
            _facilityContext = facilityContext;
            _modalNavigationStore = modalNavigationStore;
            _mediator = mediator;
            _turnstileService = turnstileService;
            _gymOperationService = gymOperationService;
            _salonService = salonService;

            Title = "Quick Registration";
        }

        public async override Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            _turnstileService.CardScanned += OnCardScanned;
            await LoadPlansAsync();
        }

        private void OnCardScanned(object? sender, Domain.Events.TurnstileScanEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                CardId = e.CardId;
                _toastService?.ShowInfo($"Card Scanned: {e.CardId}", "RFID Captured");
            });
        }

        private async Task LoadPlansAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                if (_facilityContext.CurrentFacility == FacilityType.Salon)
                {
                    await _salonService.LoadServicesAsync();
                    var mappedPlans = _salonService.Services.Select(s => new MembershipPlanDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Price = s.BasePrice,
                        DurationDays = 365, // Salon "plans" are just services, default 1 year member status
                        IsActive = true
                    }).ToList();
                    
                    Plans = new ObservableCollection<MembershipPlanDto>(mappedPlans);
                }
                else
                {
                    var result = await _planService.GetAllPlansAsync(_facilityContext.CurrentFacilityId);
                    if (result.IsSuccess)
                    {
                        var membershipPlans = result.Value.FindAll(p => !p.IsSessionPack);
                        Plans = new ObservableCollection<MembershipPlanDto>(membershipPlans);
                    }
                }
            });
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                _toastService?.ShowError("Full name is required.");
                return;
            }

            await ExecuteLoadingAsync(async () =>
            {
                var member = new MemberDto
                {
                    FullName = FullName,
                    Email = Email,
                    PhoneNumber = PhoneNumber,
                    CardId = CardId,
                    Gender = Gender,
                    MembershipPlanId = SelectedPlan?.Id,
                    MembershipPlanName = SelectedPlan?.Name ?? "Walk-In",
                    Status = MemberStatus.Active,
                    StartDate = DateTime.UtcNow,
                    ExpirationDate = DateTime.UtcNow.AddDays(SelectedPlan?.DurationDays ?? 30) // Use plan duration or default 30 days
                };

                var result = await _memberService.CreateMemberAsync(_facilityContext.CurrentFacilityId, member);

                if (result.IsSuccess)
                {
                    _turnstileService.CardScanned -= OnCardScanned;

                    // Note: Sale recording is automatically handled by the backend CreateMemberCommandHandler.
                    // Doing it here caused duplicate revenue entries on the Dashboard.


                    await _mediator.Publish(new FacilityActionCompletedNotification(
                        _facilityContext.CurrentFacilityId,
                        "Registration",
                        FullName,
                        $"Successfully registered {FullName}"));

                    // Notify ViewModels to refresh (Dirty Flag)
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Member>(_facilityContext.CurrentFacilityId));
                    if (SelectedPlan != null && SelectedPlan.Price > 0)
                    {
                        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Sale>(_facilityContext.CurrentFacilityId));
                    }

                    await _modalNavigationStore.CloseAsync(ModalResult.Success(result.Value));
                }
                else
                {
                    _toastService?.ShowError("Registration failed: " + result.Error);
                }
            }, "Registration failed.");
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            _turnstileService.CardScanned -= OnCardScanned;
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }
    }
}
