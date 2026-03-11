using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.Enums;
using System;

namespace Management.Presentation.ViewModels.PointOfSale
{
    public partial class PaymentViewModel : ObservableObject
    {
        public decimal TotalAmount { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RemainingAmount))]
        [NotifyPropertyChangedFor(nameof(ChangeAmount))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        [NotifyCanExecuteChangedFor(nameof(FinalizePaymentCommand))]
        private decimal _cashAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RemainingAmount))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        [NotifyCanExecuteChangedFor(nameof(FinalizePaymentCommand))]
        private decimal _cardAmount;

        public decimal RemainingAmount => Math.Max(0, TotalAmount - (CashAmount + CardAmount));
        public decimal ChangeAmount => Math.Max(0, (CashAmount + CardAmount) - TotalAmount);

        public bool IsValid => RemainingAmount == 0;

        public PaymentViewModel(decimal totalAmount)
        {
            TotalAmount = totalAmount;
        }

        [RelayCommand(CanExecute = nameof(IsValid))]
        private void FinalizePayment()
        {
            // Logic handled by parent via interaction or event, or strict MVVM via messenger.
            // For simplicity here, we'll let the parent observe this or trigger an action.
            // Actually, the parent (PosViewModel) will execute this command logic or bind to it.
            // Let's make this command just signal completion if we were using a dialogue service.
            // But since we are likely embedding this VM in the parent, the parent can bind a command to this VM's state?
            // Better: The command executes, and we trigger an event or callback.
            OnPaymentFinalized?.Invoke();
        }

        public event Action? OnPaymentFinalized;

        public PaymentMethod GetresultingPaymentMethod()
        {
            if (CardAmount > 0 && CashAmount > 0) return PaymentMethod.Mixed;
            if (CardAmount > 0) return PaymentMethod.CreditCard;
            return PaymentMethod.Cash;
        }
    }
}
