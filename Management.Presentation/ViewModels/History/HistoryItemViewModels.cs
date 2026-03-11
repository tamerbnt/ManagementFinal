using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Management.Presentation.ViewModels.History
{
    public abstract partial class HistoryEventItemViewModel : ObservableObject
    {
        [ObservableProperty] private Guid _id;
        [ObservableProperty] private bool _isActive;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private string _details = string.Empty;
        [ObservableProperty] private DateTime _timestamp;
        [ObservableProperty] private bool _isSuccessful = true;
        [ObservableProperty] private string? _auditNote;

        public IRelayCommand ViewDetailsCommand { get; }
        public IRelayCommand CopyDetailsCommand { get; }

        public bool HasAuditNote => !string.IsNullOrWhiteSpace(AuditNote);

        protected HistoryEventItemViewModel(HistoryViewModel parent)
        {
            ViewDetailsCommand = new RelayCommand(() => {
                parent.SelectedEvent = this;
                parent.IsDetailOpen = true;
            });
            CopyDetailsCommand = new RelayCommand(() => {
                // Copy logic
            });
        }
    }

    public partial class AccessEventItemViewModel : HistoryEventItemViewModel
    {
        public AccessEventItemViewModel(HistoryViewModel parent) : base(parent) { }
    }

    public partial class PaymentEventItemViewModel : HistoryEventItemViewModel
    {
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _paymentMethod = "Visa";

        public PaymentEventItemViewModel(HistoryViewModel parent) : base(parent) { }
    }

    public partial class SaleEventItemViewModel : HistoryEventItemViewModel
    {
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _itemsSummary = string.Empty;

        public SaleEventItemViewModel(HistoryViewModel parent) : base(parent) { }
    }

    public partial class ReservationEventItemViewModel : HistoryEventItemViewModel
    {
        public ReservationEventItemViewModel(HistoryViewModel parent) : base(parent) { }
    }

    public partial class AppointmentEventItemViewModel : HistoryEventItemViewModel
    {
        public AppointmentEventItemViewModel(HistoryViewModel parent) : base(parent) { }
    }

    public partial class PayrollEventItemViewModel : HistoryEventItemViewModel
    {
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _paymentMethod = string.Empty;

        public PayrollEventItemViewModel(HistoryViewModel parent) : base(parent) { }
    }

    public partial class InventoryEventItemViewModel : HistoryEventItemViewModel
    {
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _resourceName = string.Empty;

        public InventoryEventItemViewModel(HistoryViewModel parent) : base(parent) { }
    }
}
