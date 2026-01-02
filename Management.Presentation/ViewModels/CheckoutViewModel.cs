using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels;

namespace Management.Presentation.ViewModels
{
    public class CheckoutViewModel : ViewModelBase, INavigationAware
    {
        private readonly ISaleService _saleService;
        private readonly SaleStore _saleStore;
        private readonly ModalNavigationStore _modalStore;
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;

        public string TerminologyTitle => _facilityContext.CurrentFacility == Management.Domain.Enums.FacilityType.Salon ? "Client" : "Member";

        // --- STATE ---
        public decimal TotalDue => _saleStore.TotalAmount;

        // Payment Methods (Mutually Exclusive)
        private bool _isCashSelected = true;
        public bool IsCashSelected
        {
            get => _isCashSelected;
            set { if (SetProperty(ref _isCashSelected, value) && value) UpdateMethod(PaymentMethod.Cash); }
        }

        private bool _isCardSelected;
        public bool IsCardSelected
        {
            get => _isCardSelected;
            set { if (SetProperty(ref _isCardSelected, value) && value) UpdateMethod(PaymentMethod.CreditCard); }
        }

        private bool _isAccountSelected;
        public bool IsAccountSelected
        {
            get => _isAccountSelected;
            set { if (SetProperty(ref _isAccountSelected, value) && value) UpdateMethod(PaymentMethod.Account); }
        }

        private void UpdateMethod(PaymentMethod method)
        {
            _currentMethod = method;
            IsCardSelected = method == PaymentMethod.CreditCard;
            IsAccountSelected = method == PaymentMethod.Account;
            IsCashSelected = method == PaymentMethod.Cash;

            // Auto-fill tender for Card/Account
            if (!IsCashSelected) AmountTendered = TotalDue;
            else AmountTendered = 0;
        }

        private PaymentMethod _currentMethod = PaymentMethod.Cash;

        // Cash Logic
        private decimal _amountTendered;
        public decimal AmountTendered
        {
            get => _amountTendered;
            set
            {
                if (SetProperty(ref _amountTendered, value))
                    OnPropertyChanged(nameof(ChangeDue));
            }
        }

        public decimal ChangeDue => Math.Max(0, AmountTendered - TotalDue);

        // Account Logic
        public MemberDto? SelectedMemberForCharge { get; set; } // Bound to ComboBox
        public IEnumerable<MemberDto> Members { get; private set; } = Enumerable.Empty<MemberDto>(); // Populated via Service if Account selected

        // Quick Tender Options
        public List<QuickTenderOption> QuickTenderOptions { get; }

        // --- COMMANDS ---
        public ICommand CompleteSaleCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand QuickTenderCommand { get; }

        public CheckoutViewModel(
            ISaleService saleService,
            SaleStore saleStore, // Added
            ModalNavigationStore modalStore,
            INotificationService notificationService,
            Management.Domain.Services.IFacilityContextService facilityContext) // Added context
        {
            _saleService = saleService;
            _saleStore = saleStore;
            _modalStore = modalStore;
            _notificationService = notificationService;
            _facilityContext = facilityContext;

            CompleteSaleCommand = new AsyncRelayCommand(ExecuteCompleteSaleAsync);
            CancelCommand = new RelayCommand(() => _modalStore.Close());
            QuickTenderCommand = new RelayCommand<decimal>(amount => AmountTendered = amount);

            // Setup Quick Buttons
            QuickTenderOptions = new List<QuickTenderOption>
            {
                new QuickTenderOption { Amount = 10, DisplayAmount = "$10" },
                new QuickTenderOption { Amount = 20, DisplayAmount = "$20" },
                new QuickTenderOption { Amount = 50, DisplayAmount = "$50" },
                new QuickTenderOption { Amount = 100, DisplayAmount = "$100" }
            };
        }

        public async Task OnNavigatedTo(object parameter)
        {
            // Refresh totals from Store
            OnPropertyChanged(nameof(TotalDue));

            // If parameters passed (e.g. member context), handle here
            await Task.CompletedTask;
        }

        public Task OnNavigatedFrom() => Task.CompletedTask;

        private async Task ExecuteCompleteSaleAsync()
        {
            // 1. Validation
            if (IsCashSelected && AmountTendered < TotalDue)
            {
                _notificationService.ShowError("Insufficient funds tendered.");
                return;
            }

            // 2. Build Request
            var request = new CheckoutRequestDto(
                _currentMethod,
                AmountTendered,
                SelectedMemberForCharge?.Id,
                _saleStore.CurrentItems.ToDictionary(k => k.Product.Id, v => v.Quantity)
            );

            // 3. Process
            try
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                var result = await _saleService.ProcessCheckoutAsync(facilityId, request);
                if (result.IsSuccess)
                {
                    _saleStore.Clear();
                    _modalStore.Close();
                    _notificationService.ShowSuccess("Transaction Complete");
                }
                else
                {
                    _notificationService.ShowError("Transaction Failed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error: {ex.Message}");
            }
        }
    }

    // Helper for UI Binding
    public class QuickTenderOption
    {
        public required decimal Amount { get; set; }
        public required string DisplayAmount { get; set; }
    }
}