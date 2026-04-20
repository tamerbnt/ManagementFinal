using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Helpers;
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
        private readonly IPricingService _pricingService;

        private Guid? _originalPlanId;
        private DateTime _originalExpirationDate;
        private CancellationTokenSource? _leadSearchCts;


        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _cardId = string.Empty;

        [ObservableProperty]
        private int? _age;

        [ObservableProperty]
        private string _source = "Walk-in"; // Default source

        [ObservableProperty]
        private MembershipPlanDto? _selectedPlan;

        [ObservableProperty]
        private ObservableRangeCollection<MembershipPlanDto> _plans = new();

        [ObservableProperty]
        private bool _isRenewMode;

        [ObservableProperty]
        private Guid? _memberIdToUpdate;

        [ObservableProperty]
        private Gender _gender = Gender.Male; // Default to Male

        public ObservableCollection<Gender> GenderOptions { get; } = new() { Gender.Male, Gender.Female };

        [ObservableProperty]
        private ObservableRangeCollection<Management.Domain.Models.Salon.SalonService> _salonServices = new();

        [ObservableProperty]
        private Management.Domain.Models.Salon.SalonService? _selectedSalonService;

        [ObservableProperty]
        private decimal _totalPrice;

        [ObservableProperty]
        private bool _isSalonFacility;

        [ObservableProperty]
        private decimal? _originalTotalPrice;

        [ObservableProperty]
        private string? _appliedPromotionName;

        public bool IsDiscounted => AppliedPromotionName != null;
 
        // Lead Conversion
        [ObservableProperty]
        private string _leadSearchQuery = string.Empty;
 
        [ObservableProperty]
        private ObservableRangeCollection<MemberDto> _leadResults = new();
 
        [ObservableProperty]
        private MemberDto? _selectedLead;
 
        [ObservableProperty]
        private bool _hasLeadResults;
 
        [ObservableProperty]
        private bool _isSearchingLeads;
 
        partial void OnLeadSearchQueryChanged(string value)
        {
            _leadSearchCts?.Cancel();
            _leadSearchCts = new CancellationTokenSource();
            var token = _leadSearchCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token);
                    if (token.IsCancellationRequested) return;
                    await SearchLeadsCommand.ExecuteAsync(null);
                }
                catch (TaskCanceledException) { }
            }, token);
        }
 
        partial void OnSelectedLeadChanged(MemberDto? value)
        {
            if (value != null)
            {
                FullName = value.FullName;
                PhoneNumber = value.PhoneNumber ?? string.Empty;
                Email = value.Email ?? string.Empty;
                Source = value.Source ?? "Walk-in";
                
                // Set ID for conversion (acts as an update)
                MemberIdToUpdate = value.Id;
                IsRenewMode = true;

                LeadResults.Clear();
                HasLeadResults = false;
                LeadSearchQuery = string.Empty;
            }
            TriggerPriceUpdate();
        }
 
        [RelayCommand]
        private async Task SearchLeadsAsync()
        {
            if (string.IsNullOrWhiteSpace(LeadSearchQuery)) return;
 
            IsSearchingLeads = true;
            try
            {
                var result = await _memberService.SearchLeadAsync(_facilityContext.CurrentFacilityId, LeadSearchQuery);
                if (result.IsSuccess)
                {
                    LeadResults.ReplaceRange(result.Value);
                    HasLeadResults = LeadResults.Any();
                }
            }
            finally
            {
                IsSearchingLeads = false;
            }
        }

        private System.Threading.CancellationTokenSource? _pricingDebounceCts;

        partial void OnGenderChanged(Gender value) => TriggerPriceUpdate();
        partial void OnSelectedPlanChanged(MembershipPlanDto? value) => TriggerPriceUpdate();
        partial void OnSelectedSalonServiceChanged(Management.Domain.Models.Salon.SalonService? value) => TriggerPriceUpdate();

        private void TriggerPriceUpdate()
        {
            _pricingDebounceCts?.Cancel();
            _pricingDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _pricingDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(350, token);
                    if (!token.IsCancellationRequested)
                    {
                        await UpdateTotalPriceBatchAsync();
                    }
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        private async Task UpdateTotalPriceBatchAsync()
        {
            decimal effectiveTotal = 0;
            decimal originalTotal = 0;
            string? promoName = null;

            var itemsToPrice = new List<(Guid Id, Management.Domain.ValueObjects.Money Price)>();

            if (SelectedPlan != null && SelectedPlan.Id != Guid.Empty)
            {
                itemsToPrice.Add((SelectedPlan.Id, new Management.Domain.ValueObjects.Money(SelectedPlan.Price, "DA")));
            }

            if (SelectedSalonService != null && SelectedSalonService.Id != Guid.Empty)
            {
                itemsToPrice.Add((SelectedSalonService.Id, new Management.Domain.ValueObjects.Money(SelectedSalonService.BasePrice, "DA")));
            }

            if (itemsToPrice.Any())
            {
                var dict = await _pricingService.CalculateBatchPricesAsync(
                    _facilityContext.CurrentFacilityId,
                    itemsToPrice,
                    Gender);

                if (SelectedPlan != null && dict.TryGetValue(SelectedPlan.Id, out var planResult))
                {
                    effectiveTotal += planResult.EffectivePrice.Amount;
                    originalTotal += planResult.OriginalPrice.Amount;
                    if (planResult.IsDiscountApplied) promoName = planResult.AppliedPromotionName;
                }

                if (SelectedSalonService != null && dict.TryGetValue(SelectedSalonService.Id, out var svcResult))
                {
                    effectiveTotal += svcResult.EffectivePrice.Amount;
                    originalTotal += svcResult.OriginalPrice.Amount;
                    if (svcResult.IsDiscountApplied) promoName = svcResult.AppliedPromotionName;
                }
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() => 
                {
                    TotalPrice = effectiveTotal;
                    OriginalTotalPrice = (originalTotal > effectiveTotal) ? originalTotal : null;
                    AppliedPromotionName = promoName;
                });
            }
            else
            {
                TotalPrice = effectiveTotal;
                OriginalTotalPrice = (originalTotal > effectiveTotal) ? originalTotal : null;
                AppliedPromotionName = promoName;
            }
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
            ISaleService saleService,
            IPricingService pricingService)
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
            _pricingService = pricingService;

            _isSalonFacility = _facilityContext.CurrentFacility == FacilityType.Salon;
            Title = _terminologyService.GetTerm("Terminology.Modal.QuickRegistration.Title") ?? "Quick Registration";
        }

        public async override Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            _turnstileService.CardScanned += OnCardScanned;
            
            // Allow UI visual tree to paint before data binding blocks the thread
            await Task.Delay(50, cancellationToken);
            
            // Parallelize initial data loading
            var loadTasks = new List<Task> { LoadPlansAndServicesAsync() };

            if (parameter is Guid memberId)
            {
                IsRenewMode = true;
                MemberIdToUpdate = memberId;
                loadTasks.Add(LoadMemberDetailsAsync(memberId));
            }
            else if (parameter is QuickRegistrationPrefillData prefillData)
            {
                FullName = prefillData.FullName;
                Email = prefillData.Email;
                PhoneNumber = prefillData.PhoneNumber;
                Gender = prefillData.Gender;
            }

            await Task.WhenAll(loadTasks);

            // Initialize Sources
            Sources.ReplaceRange(new[] { "Walk-in", "Word of Mouth", "Instagram", "TikTok", "Facebook" });
        }

        public ObservableRangeCollection<string> Sources { get; } = new ObservableRangeCollection<string>();

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
                    if (result.Value.DateOfBirth.HasValue)
                    {
                        Age = (int)((DateTime.UtcNow - result.Value.DateOfBirth.Value).TotalDays / 365.25);
                    }
                    Source = result.Value.Source ?? "Walk-in";
                    
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

        private async Task LoadPlansAndServicesAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                var tasks = new List<Task>();

                tasks.Add(Task.Run(async () => 
                {
                    var planResult = await _planService.GetAllPlansAsync(_facilityContext.CurrentFacilityId);
                    if (planResult.IsSuccess)
                    {
                        var membershipPlans = planResult.Value.FindAll(p => !p.IsSessionPack);
                        var plansToAdd = new List<MembershipPlanDto>
                        {
                            new MembershipPlanDto { Id = Guid.Empty, Name = _terminologyService.GetTerm("Terminology.Salon.Booking.NoMembershipPlan") ?? "No Membership Plan", Price = 0 }
                        };
                        plansToAdd.AddRange(membershipPlans);
                        var dispatcher = System.Windows.Application.Current?.Dispatcher;
                        if (dispatcher != null)
                            await dispatcher.InvokeAsync(() => Plans.ReplaceRange(plansToAdd));
                        else
                            Plans.ReplaceRange(plansToAdd);
                    }
                }));

                // If Salon, also load Salon Services
                if (IsSalonFacility)
                {
                    tasks.Add(Task.Run(async () => 
                    {
                        await _salonService.LoadServicesAsync();
                        var servicesToAdd = new List<Management.Domain.Models.Salon.SalonService>
                        {
                            new Management.Domain.Models.Salon.SalonService { Id = Guid.Empty, Name = _terminologyService.GetTerm("Terminology.Salon.Booking.NoService") ?? "No Service", BasePrice = 0 }
                        };
                        servicesToAdd.AddRange(_salonService.Services);
                        var dispatcher = System.Windows.Application.Current?.Dispatcher;
                        if (dispatcher != null)
                            await dispatcher.InvokeAsync(() => SalonServices.ReplaceRange(servicesToAdd));
                        else
                            SalonServices.ReplaceRange(servicesToAdd);
                    }));
                }

                await Task.WhenAll(tasks);
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
                    DateOfBirth = Age.HasValue ? DateTime.UtcNow.AddYears(-Age.Value) : (DateTime?)null,
                    Source = Source,
                    // FIX: In Salon mode the handler uses dto.MembershipPlanId to look up a SalonService
                    // (via ISalonServiceRepository). We must supply the SalonService ID, not a MembershipPlan ID.
                    // For Gym mode, selectedServiceId is always null, so selectedPlanId is used as before.
                    MembershipPlanId = IsSalonFacility ? (selectedServiceId ?? selectedPlanId) : selectedPlanId,
                    MembershipPlanName = IsSalonFacility
                        ? (SelectedSalonService?.Id != Guid.Empty ? SelectedSalonService?.Name : SelectedPlan?.Name)
                        : (SelectedPlan?.Id == Guid.Empty ? "Walk-In" : SelectedPlan?.Name),
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
                    // REVENUE TRACKING: Now handled centrally in CreateMemberCommandHandler.
                    // Redundant ViewModel-side recording removed to prevent duplicate sales and ChangeTracker poisoning.

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
