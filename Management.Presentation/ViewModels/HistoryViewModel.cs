using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Collections.ObjectModel;
using Management.Application.Services;
using System.ComponentModel; // For ICollectionView
using System.Linq;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using System.Windows.Data; // For CollectionViewSource
using System.Windows.Input;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Presentation.Services;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using System.Windows.Threading;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using Management.Application.Features.History.Queries.GetUnifiedHistory;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly IMediator _mediator;
        private readonly IReservationService _reservationService; // Still needed for cancellation
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly ITerminologyService _terminologyService;

        // Master Collection
        private readonly ObservableCollection<HistoryEventItemViewModel> _historyEvents;

        // Public View (Supports Grouping & Filtering)
        public ICollectionView HistoryEventsView { get; }

        // --- 1. FILTER STATE ---

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    HistoryEventsView.Refresh(); 
            }
        }

        private DateTime _startDate;
        private DateTime _endDate;

        public string DateRangeText => $"{_startDate:MMM d} - {_endDate:MMM d}";

        // --- 2. COMMANDS ---

        public ICommand SelectDateRangeCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand PrintReportCommand { get; }

        // --- 3. CONSTRUCTOR ---

        public HistoryViewModel(
            IMediator mediator,
            IReservationService reservationService,
            IDialogService dialogService,
            INotificationService notificationService,
            ITerminologyService terminologyService)
        {
            _mediator = mediator;
            _reservationService = reservationService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _terminologyService = terminologyService;

            // Default Range: Last 30 Days
            _endDate = DateTime.Now;
            _startDate = DateTime.Now.AddDays(-30);

            // Initialize Collection & View
            _historyEvents = new ObservableCollection<HistoryEventItemViewModel>();
            HistoryEventsView = CollectionViewSource.GetDefaultView(_historyEvents);

            // Setup Grouping by Date
            HistoryEventsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HistoryEventItemViewModel.DateGroupHeader)));

            // Setup Sorting (Newest First)
            HistoryEventsView.SortDescriptions.Add(new SortDescription(nameof(HistoryEventItemViewModel.Timestamp), ListSortDirection.Descending));

            // Setup Filtering
            HistoryEventsView.Filter = FilterHistoryEvents;

            // Initialize Commands
            SelectDateRangeCommand = new RelayCommand(ExecuteSelectDateRange);
            ExportCommand = new RelayCommand(ExecuteExport);
            PrintReportCommand = new RelayCommand(ExecutePrint);

            // Initial Load
            _ = LoadDataAsync();
        }

        // --- 4. DATA AGGREGATION ENGINE ---

        private async Task LoadDataAsync()
        {
            try
            {
                _historyEvents.Clear();

                // ARCHITECTURAL FIX: MediatR Query replaces manual service orchestration.
                // Feature Handler manages multi-tenant context internally.
                var query = new GetUnifiedHistoryQuery(_startDate, _endDate);
                var unifiedEvents = await _mediator.Send(query);

                foreach (var ev in unifiedEvents)
                {
                    switch (ev.Type)
                    {
                        case HistoryEventType.Access:
                            _historyEvents.Add(new AccessEventItemViewModel(ev.AccessEvent!, _notificationService, _terminologyService));
                            break;
                        case HistoryEventType.Sale:
                            _historyEvents.Add(new PaymentEventItemViewModel(ev.SaleEvent!, _notificationService));
                            break;
                        case HistoryEventType.Reservation:
                            _historyEvents.Add(new ReservationEventItemViewModel(ev.ReservationEvent!, _reservationService, _dialogService, RefreshDataCallback, _terminologyService));
                            break;
                    }
                }

                OnPropertyChanged(nameof(HistoryEventsView));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to load history: {ex.Message}");
            }
        }

        // Callback to refresh data after an action
        private async void RefreshDataCallback()
        {
            await LoadDataAsync();
        }

        // --- 5. FILTER LOGIC ---

        private bool FilterHistoryEvents(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            if (obj is HistoryEventItemViewModel item)
            {
                // Polymorphic Search: Check Title and Details
                return item.Title.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.Details.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        // --- 6. ACTIONS ---

        private void ExecuteSelectDateRange()
        {
            // In a real app, open a Dialog to pick dates.
            // For now, toggle to "All Time" simulation
            _startDate = DateTime.Now.AddYears(-1);
            OnPropertyChanged(nameof(DateRangeText));
            _ = LoadDataAsync();
        }

        private void ExecuteExport() { /* Call ExportService */ }
        private void ExecutePrint() { /* Call PrintService */ }
    }

    // =========================================================================
    // POLYMORPHIC VIEW MODEL HIERARCHY
    // =========================================================================

    public abstract class HistoryEventItemViewModel : ViewModelBase
    {
        public Guid Id { get; }
        public DateTime Timestamp { get; }

        // Shared properties for binding
        public abstract string Title { get; }
        public abstract string Details { get; }

        protected readonly ITerminologyService _terminologyService;

        public string DateGroupHeader
        {
            get
            {
                var date = Timestamp.Date;
                if (date == DateTime.Today) return _terminologyService?.GetTerm("Today") ?? "Today";
                if (date == DateTime.Today.AddDays(-1)) return _terminologyService?.GetTerm("Yesterday") ?? "Yesterday";
                return date.ToString("MMMM d, yyyy");
            }
        }

        protected HistoryEventItemViewModel(Guid id, DateTime timestamp, ITerminologyService terminologyService)
        {
            Id = id;
            Timestamp = timestamp;
            _terminologyService = terminologyService;
        }
    }

    public class AccessEventItemViewModel : HistoryEventItemViewModel
    {
        private readonly AccessEventDto _dto;
        private readonly INotificationService _notificationService;

        public bool IsSuccessful => _dto.IsAccessGranted;
        public override string Title => _dto.MemberName;
        public override string Details => $"{_dto.FacilityName}   {_dto.AccessStatus}";

        public ICommand CopyDetailsCommand { get; }

        public AccessEventItemViewModel(AccessEventDto dto, INotificationService notificationService, ITerminologyService terminologyService)
            : base(dto.Id, dto.Timestamp, terminologyService)
        {
            _dto = dto;
            _notificationService = notificationService;
            CopyDetailsCommand = new RelayCommand(ExecuteCopy);
        }

        private void ExecuteCopy()
        {
            System.Windows.Clipboard.SetText($"{Title} - {Details} at {Timestamp}");
            _notificationService.ShowSuccess("Copied to clipboard");
        }
    }

    public class PaymentEventItemViewModel : HistoryEventItemViewModel
    {
        private readonly SaleDto _dto;
        private readonly INotificationService _notificationService;

        public decimal Amount => _dto.TotalAmount;
        public string PaymentMethod => _dto.PaymentMethod; // "Visa", "Cash"

        public override string Title => _dto.TransactionType; // "Membership Renewal"
        public override string Details => $"{Amount:C}   {PaymentMethod}";

        public ICommand CopyReceiptCommand { get; }

        public PaymentEventItemViewModel(SaleDto dto, INotificationService notificationService)
            : base(dto.Id, dto.Timestamp, null!) // Terminology not strictly needed for payments yet
        {
            _dto = dto;
            _notificationService = notificationService;
            CopyReceiptCommand = new RelayCommand(ExecuteCopyReceipt);
        }

        private void ExecuteCopyReceipt()
        {
            // Logic to copy receipt ID or details
            _notificationService.ShowSuccess("Receipt info copied");
        }
    }

    public class ReservationEventItemViewModel : HistoryEventItemViewModel
    {
        private readonly ReservationDto _dto;
        private readonly IReservationService _service;
        private readonly IDialogService _dialogService;
        private readonly Action _refreshCallback;

        public string InstructorName => _dto.InstructorName;
        public string Location => _dto.Location;

        public override string Title => _dto.ActivityName; // "Personal Training"
        public override string Details => $"with {InstructorName}   {Location}";

        public ICommand ViewDetailsCommand { get; }
        public ICommand CancelBookingCommand { get; }

        public ReservationEventItemViewModel(
            ReservationDto dto,
            IReservationService service,
            IDialogService dialogService,
            Action refreshCallback,
            ITerminologyService terminologyService)
            : base(dto.Id, dto.StartTime, terminologyService)
        {
            _dto = dto;
            _service = service;
            _dialogService = dialogService;
            _refreshCallback = refreshCallback;

            CancelBookingCommand = new AsyncRelayCommand(ExecuteCancelAsync);
            ViewDetailsCommand = new RelayCommand(() => { /* Open Modal */ });
        }

        private async Task ExecuteCancelAsync()
        {
            bool confirm = await _dialogService.ShowConfirmationAsync("Cancel Booking?", "Are you sure?");
            if (confirm)
            {
                var result = await _service.CancelReservationAsync(Id);
                if (result.IsSuccess)
                {
                    _refreshCallback?.Invoke(); // Notify Parent to refresh list
                }
            }
        }
    }
}
