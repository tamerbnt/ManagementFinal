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
    public record QuickRegistrationPrefillData(
        string FullName,
        string Email,
        string PhoneNumber,
        Gender Gender
    );

    public record QuickRegistrationResult(Guid MemberId, DateTime ApprovedAt);

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
        private readonly ITerminologyService _terminologyService;
        private readonly ISaleService _saleService;

        private Guid? _originalPlanId;
        private DateTime _originalExpirationDate;


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
        private bool _isRenewMode;

        [ObservableProperty]
        private Guid? _memberIdToUpdate;

        [ObservableProperty]
        private Gender _gender = Gender.Male; // Default to Male

        public ObservableCollection<Gender> GenderOptions { get; } = new() { Gender.Male, Gender.Female };

        [ObservableProperty]
        private ObservableCollection<Management.Domain.Models.Salon.SalonService> _salonServices = new();

        [ObservableProperty]
        private Management.Domain.Models.Salon.SalonService? _selectedSalonService;

        [ObservableProperty]
        private decimal _totalPrice;

        [ObservableProperty]
        private bool _isSalonFacility;

        partial void OnSelectedPlanChanged(MembershipPlanDto? value) => UpdateTotalPrice();
        partial void OnSelectedSalonServiceChanged(Management.Domain.Models.Salon.SalonService? value) => UpdateTotalPrice();

        private void UpdateTotalPrice()
        {
            TotalPrice = (SelectedPlan?.Price ?? 0) + (SelectedSalonService?.BasePrice ?? 0);
        }

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
            Management.Presentation.Services.Salon.ISalonService salonService,
            ITerminologyService terminologyService,
            ISaleService saleService)
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
            _terminologyService = terminologyService;
            _saleService = saleService;

            _isSalonFacility = _facilityContext.CurrentFacility == FacilityType.Salon;
            Title = "Quick Registration";
        }

        public async override Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            _turnstileService.CardScanned += OnCardScanned;
            await LoadPlansAsync();

            if (parameter is Guid memberId)
            {
                IsRenewMode = true;
                MemberIdToUpdate = memberId;
                await LoadMemberDetailsAsync(memberId);
            }
            else if (parameter is QuickRegistrationPrefillData prefillData)
            {
                FullName = prefillData.FullName;
                Email = prefillData.Email;
                PhoneNumber = prefillData.PhoneNumber;
                Gender = prefillData.Gender;
            }
        }

        private async Task LoadMemberDetailsAsync(Guid memberId)
        {
            await ExecuteLoadingAsync(async () =>
            {
                var result = await _memberService.GetMemberAsync(_facilityContext.CurrentFacilityId, memberId);
                if (result.IsSuccess && result.Value != null)
                {
                    FullName = result.Value.FullName;
                    Email = result.Value.Email ?? string.Empty;
                    PhoneNumber = result.Value.PhoneNumber ?? string.Empty;
                    CardId = result.Value.CardId ?? string.Empty;
                    if (result.Value.Gender.HasValue) Gender = result.Value.Gender.Value;
                    
                    if (result.Value.MembershipPlanId.HasValue)
                    {
                        _originalPlanId = result.Value.MembershipPlanId.Value;
                        _originalExpirationDate = result.Value.ExpirationDate;
                        SelectedPlan = Plans.FirstOrDefault(p => p.Id == result.Value.MembershipPlanId.Value);
                    }
                }

            }, "Failed to load member details.");
        }

        private void OnCardScanned(object? sender, Domain.Events.TurnstileScanEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                CardId = e.CardId;
                _toastService?.ShowInfo($"Card Scanned: {e.CardId}", "RFID Captured");
            });
        }

        private async Task LoadPlansAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                // Always load normal membership plans
                var planResult = await _planService.GetAllPlansAsync(_facilityContext.CurrentFacilityId);
                if (planResult.IsSuccess)
                {
                    var membershipPlans = planResult.Value.FindAll(p => !p.IsSessionPack);
                    Plans.Clear();
                    // Add "None" option
                    Plans.Add(new MembershipPlanDto { Id = Guid.Empty, Name = _terminologyService.GetTerm("Terminology.Salon.Booking.NoMembershipPlan") ?? "No Membership Plan", Price = 0 });
                    foreach (var plan in membershipPlans)
                    {
                        Plans.Add(plan);
                    }
                }

                // If Salon, also load Salon Services
                if (IsSalonFacility)
                {
                    await _salonService.LoadServicesAsync();
                    SalonServices.Clear();
                    // Add "None" option
                    SalonServices.Add(new Management.Domain.Models.Salon.SalonService { Id = Guid.Empty, Name = _terminologyService.GetTerm("Terminology.Salon.Booking.NoService") ?? "No Service", BasePrice = 0 });
                    foreach (var s in _salonService.Services)
                    {
                        SalonServices.Add(s);
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

            var selectedPlanId = SelectedPlan?.Id == Guid.Empty ? null : SelectedPlan?.Id;
            var selectedServiceId = SelectedSalonService?.Id == Guid.Empty ? null : SelectedSalonService?.Id;

            if (selectedPlanId == null && selectedServiceId == null)
            {
                _toastService?.ShowError("Please select at least a membership plan or a service.");
                return;
            }

            await ExecuteLoadingAsync(async () =>
            {
                var member = new MemberDto
                {
                    Id = MemberIdToUpdate ?? Guid.Empty,
                    FullName = FullName,
                    Email = Email,
                    PhoneNumber = PhoneNumber,
                    CardId = CardId,
                    Gender = Gender,
                    MembershipPlanId = selectedPlanId,
                    MembershipPlanName = SelectedPlan?.Id == Guid.Empty ? (SelectedSalonService?.Id != Guid.Empty ? "Service Only" : "Walk-In") : SelectedPlan?.Name,
                    Status = MemberStatus.Active,
                    StartDate = DateTime.UtcNow,
                    ExpirationDate = (IsRenewMode && SelectedPlan?.Id == _originalPlanId && _originalExpirationDate > DateTime.UtcNow) 
                                     ? _originalExpirationDate 
                                     : DateTime.UtcNow.AddDays(SelectedPlan?.DurationDays ?? 30)
                };


                // If a salon service is selected but no plan, we might want to still create a "Walk-In" member
                // The price recorded should be the sum.
                var priceToRecord = TotalPrice;

                Management.Domain.Primitives.Result<Guid>? resultCreate = null;
                Management.Domain.Primitives.Result? resultUpdate = null;
                bool isSuccess = false;

                if (IsRenewMode)
                {
                    resultUpdate = await _memberService.UpdateMemberAsync(_facilityContext.CurrentFacilityId, member);
                    isSuccess = resultUpdate.IsSuccess;
                }
                else
                {
                    resultCreate = await _memberService.CreateMemberAsync(_facilityContext.CurrentFacilityId, member);
                    isSuccess = resultCreate.IsSuccess;
                }


                if (isSuccess)
                {
                    _turnstileService.CardScanned -= OnCardScanned;

                    // Notify ViewModels to refresh (Dirty Flag) - Now handled via Bridge from CommandHandler notifications
                    var approvedAt = DateTime.UtcNow;
                    if (priceToRecord > 0)
                    {
                        // Trigger sale recording
                        var label = selectedPlanId != null ? SelectedPlan?.Name : null;
                        if (selectedServiceId != null)
                        {
                            label = string.IsNullOrEmpty(label) ? SelectedSalonService?.Name : $"{label} + {SelectedSalonService?.Name}";
                        }

                        await _gymOperationService.SellItemAsync(
                            (IsRenewMode ? MemberIdToUpdate : resultCreate?.Value)?.ToString(),
                            priceToRecord,
                            label ?? "Registration",
                            _facilityContext.CurrentFacilityId,
                            "Registration",
                            selectedPlanId != null ? SaleCategory.Membership : SaleCategory.Service,
                            label ?? "Registration");

                        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Sale>(_facilityContext.CurrentFacilityId));
                    }

                    var memberId = IsRenewMode ? MemberIdToUpdate : resultCreate?.Value;
                    await _modalNavigationStore.CloseAsync(ModalResult.Success(new QuickRegistrationResult(memberId ?? Guid.Empty, approvedAt)));
                }
                else
                {
                    _toastService?.ShowError((IsRenewMode ? "Update failed: " + resultUpdate?.Error : "Registration failed: " + resultCreate?.Error));
                }
            }, "Operation failed.");
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            _turnstileService.CardScanned -= OnCardScanned;
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }
    }
}
