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
        private readonly MediatR.IMediator _mediator;

        [ObservableProperty]
        private ObservableCollection<WalkInPlanDto> _plans = new();

        [ObservableProperty]
        private WalkInPlanDto? _selectedPlan;

        [ObservableProperty]
        private int _guestCount = 1;

        [ObservableProperty]
        private decimal _totalPrice;

        public new string Title => "Process Walk-In";

        public WalkInConfirmationViewModel(
            IGymOperationService gymService,
            ModalNavigationStore modalNavigationStore,
            IFacilityContextService facilityContext,
            MediatR.IMediator mediator)
        {
            _gymService = gymService;
            _modalNavigationStore = modalNavigationStore;
            _facilityContext = facilityContext;
            _mediator = mediator;
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
            
            SelectedPlan = Plans.FirstOrDefault();
            UpdateTotalPrice();
        }

        partial void OnSelectedPlanChanged(WalkInPlanDto? value) => UpdateTotalPrice();
        partial void OnGuestCountChanged(int value) => UpdateTotalPrice();

        private void UpdateTotalPrice()
        {
            TotalPrice = (SelectedPlan?.Price ?? 0) * GuestCount;
        }

        [RelayCommand]
        private void IncrementGuestCount() => GuestCount++;

        [RelayCommand]
        private void DecrementGuestCount()
        {
            if (GuestCount > 1) GuestCount--;
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private async Task ConfirmWalkInAsync()
        {
            if (SelectedPlan == null) return;

            await ExecuteLoadingAsync(async () =>
            {
                var saleIds = new System.Collections.Generic.List<Guid>();

                // Process each guest entry
                for (int i = 0; i < GuestCount; i++)
                {
                    // Suppress individual notifications to prevent "Toast Storm"
                    var result = await _gymService.ProcessWalkInAsync(
                        SelectedPlan.Price, 
                        _facilityContext.CurrentFacilityId, 
                        SelectedPlan.Name, 
                        publishNotification: false);

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
