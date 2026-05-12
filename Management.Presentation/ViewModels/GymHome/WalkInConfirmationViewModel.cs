using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Presentation.Extensions; // Fix ViewModelBase
using Management.Presentation.Stores;   // Fix ModalNavigationStore
using Management.Domain.Services;

namespace Management.Presentation.ViewModels.GymHome
{
    public partial class WalkInConfirmationViewModel : ViewModelBase
    {
        private readonly IGymOperationService _gymService;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly IFacilityContextService _facilityContext;
        private readonly Management.Domain.Services.IDialogService _dialogService;
        private readonly Management.Presentation.Services.IModalNavigationService _modalNavigationService;
        private readonly MediatR.IMediator _mediator;
        private readonly IPricingService _pricingService;
        private readonly IDiscountService _discountService;

        [ObservableProperty]
        private ObservableCollection<WalkInPlanDto> _plans = new();

        [ObservableProperty]
        private WalkInPlanDto? _selectedPlan;

        [ObservableProperty]
        private int _guestCount = 1;

        [ObservableProperty]
        private decimal _totalPrice;

        [ObservableProperty]
        private ObservableCollection<DiscountDto> _availableDiscounts = new();

        [ObservableProperty]
        private DiscountDto? _selectedDiscount;

        [ObservableProperty]
        private PricingResult? _pricingResult;

        public new string Title => "Process Walk-In";

        public WalkInConfirmationViewModel(
            IGymOperationService gymService,
            ModalNavigationStore modalNavigationStore,
            IFacilityContextService facilityContext,
            Management.Domain.Services.IDialogService dialogService,
            Management.Presentation.Services.IModalNavigationService modalNavigationService,
            MediatR.IMediator mediator,
            IPricingService pricingService,
            IDiscountService discountService)
        {
            _gymService = gymService;
            _modalNavigationStore = modalNavigationStore;
            _facilityContext = facilityContext;
            _dialogService = dialogService;
            _modalNavigationService = modalNavigationService;
            _mediator = mediator;
            _pricingService = pricingService;
            _discountService = discountService;
            base.Title = "Process Walk-In";
            
            _ = InitializeAsync();
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }

        private async Task InitializeAsync()
        {
            var plans = await _gymService.GetWalkInPlansAsync(_facilityContext.CurrentFacilityId);
            Plans = new ObservableCollection<WalkInPlanDto>(plans);
            
            var discounts = await _discountService.GetDiscountsAsync(_facilityContext.CurrentFacilityId);
            if (discounts.IsSuccess)
            {
                AvailableDiscounts = new ObservableCollection<DiscountDto>(discounts.Value.Where(d => d.IsActive));
            }

            SelectedPlan = Plans.FirstOrDefault();
            await UpdatePriceAsync();
        }

        partial void OnSelectedPlanChanged(WalkInPlanDto? value) => _ = UpdatePriceAsync();
        partial void OnGuestCountChanged(int value) => _ = UpdatePriceAsync();
        partial void OnSelectedDiscountChanged(DiscountDto? value) => _ = UpdatePriceAsync();

        private async Task UpdatePriceAsync()
        {
            if (SelectedPlan == null)
            {
                TotalPrice = 0;
                PricingResult = null;
                return;
            }

            var basePrice = new Management.Domain.ValueObjects.Money(SelectedPlan.Price, "DA");
            decimal? manualVal = null;
            bool isPerc = false;
            if (SelectedDiscount != null)
            {
                isPerc = SelectedDiscount.IsPercentage;
                manualVal = SelectedDiscount.Value;
            }

            var result = await _pricingService.CalculateEffectivePriceAsync(
                _facilityContext.CurrentFacilityId,
                Guid.Empty,
                basePrice,
                manualDiscountValue: manualVal,
                isManualDiscountPercentage: isPerc);

            PricingResult = result;
            TotalPrice = result.EffectivePrice.Amount * GuestCount;
        }

        [RelayCommand]
        private void IncrementGuestCount() => GuestCount++;

        [RelayCommand]
        private void DecrementGuestCount()
        {
            if (GuestCount > 1) GuestCount--;
        }

        [RelayCommand]
        private async Task OpenRegisterWalkInAsync()
        {
            // Close current modal and open registration modal
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
            await _modalNavigationService.OpenModalAsync<RegisterWalkInViewModel>(Management.Presentation.Services.ModalSize.Small);
        }

        [RelayCommand]
        private async Task CancelWalkInAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private async Task ConfirmWalkInAsync()
        {
            if (SelectedPlan == null || PricingResult == null) return;

            await ExecuteLoadingAsync(async () =>
            {
                var saleIds = new System.Collections.Generic.List<Guid>();

                // Process each guest entry
                for (int i = 0; i < GuestCount; i++)
                {
                    // Suppress individual notifications to prevent "Toast Storm"
                    var result = await _gymService.ProcessWalkInAsync(
                        PricingResult.EffectivePrice.Amount, 
                        _facilityContext.CurrentFacilityId, 
                        SelectedPlan.Name, 
                        publishNotification: false,
                        manualDiscountId: SelectedDiscount?.Id,
                        manualDiscountAmount: PricingResult.ManualDiscountAmount?.Amount);

                    if (result.Success)
                    {
                        saleIds.Add(result.SaleId);
                    }
                }

                if (saleIds.Any())
                {
                    // Publish ONE composite notification for the whole batch
                    var batchIdString = string.Join(",", saleIds);
                    await _mediator.Publish(new Management.Application.Notifications.FacilityActionCompletedNotification(
                        _facilityContext.CurrentFacilityId,
                        "Walk-In",
                        $"{GuestCount} Guests",
                        $"Processed {GuestCount} walk-in guests for {TotalPrice:N0} DA",
                        batchIdString));
                }

                await _modalNavigationStore.CloseAsync(ModalResult.Success(new { Count = GuestCount, Plan = SelectedPlan.Name }));
            }, "Failed to process walk-in.");
        }

        private bool CanConfirm()
        {
            return SelectedPlan != null && GuestCount > 0;
        }
    }
}
