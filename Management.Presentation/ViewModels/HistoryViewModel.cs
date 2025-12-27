using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // For ICollectionView
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data; // For CollectionViewSource
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.DTOs;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly IAccessEventService _accessEventService;
        private readonly ISaleService _saleService;
        private readonly IReservationService _reservationService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;

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
                    HistoryEventsView.Refresh(); // Client-side filter
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
            IAccessEventService accessEventService,
            ISaleService saleService,
            IReservationService reservationService,
            IDialogService dialogService,
            INotificationService notificationService)
        {
            _accessEventService = accessEventService;
            _saleService = saleService;
            _reservationService = reservationService;
            _dialogService = dialogService;
            _notificationService = notificationService;

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
            SelectDateRangeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteSelectDateRange);
            ExportCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteExport);
            PrintReportCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecutePrint);

            // Initial Load
            _ = LoadDataAsync();
        }

        // --- 4. DATA AGGREGATION ENGINE ---

        private async Task LoadDataAsync()
        {
            try
            {
                _historyEvents.Clear();

                // Fetch all streams in parallel for performance
                var t1 = _accessEventService.GetEventsByRangeAsync(_startDate, _endDate);
                var t2 = _saleService.GetSalesByRangeAsync(_startDate, _endDate);
                var t3 = _reservationService.GetReservationsByRangeAsync(_startDate, _endDate);

                await Task.WhenAll(t1, t2, t3);

                var accessLogs = t1.Result;
                var salesLogs = t2.Result;
                var bookingLogs = t3.Result;

                // 1. Map Access Events
                foreach (var dto in accessLogs)
                {
                    _historyEvents.Add(new AccessEventItemViewModel(dto, _notificationService));
                }

                // 2. Map Sales Events
                foreach (var dto in salesLogs)
                {
                    _historyEvents.Add(new PaymentEventItemViewModel(dto, _notificationService));
                }

                // 3. Map Reservations
                foreach (var dto in bookingLogs)
                {
                    _historyEvents.Add(new ReservationEventItemViewModel(dto, _reservationService, _dialogService, RefreshDataCallback));
                }

                // Notify UI of count change (for Empty State binding)
                OnPropertyChanged(nameof(HistoryEventsView));
            }
            catch (Exception)
            {
                // _notificationService.ShowError("Failed to load history.");
            }
        }

        // Callback to refresh data after an action (like Cancel Booking)
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

        // Logic for Group Headers (e.g. "Today", "Yesterday", "Nov 23")
        public string DateGroupHeader
        {
            get
            {
                var date = Timestamp.Date;
                if (date == DateTime.Today) return "Today";
                if (date == DateTime.Today.AddDays(-1)) return "Yesterday";
                return date.ToString("MMMM d, yyyy");
            }
        }

        protected HistoryEventItemViewModel(Guid id, DateTime timestamp)
        {
            Id = id;
            Timestamp = timestamp;
        }
    }

    public class AccessEventItemViewModel : HistoryEventItemViewModel
    {
        private readonly AccessEventDto _dto;
        private readonly INotificationService _notificationService;

        public bool IsSuccessful => _dto.IsAccessGranted;
        public override string Title => _dto.MemberName;
        public override string Details => $"{_dto.FacilityName} • {_dto.AccessStatus}";

        public ICommand CopyDetailsCommand { get; }

        public AccessEventItemViewModel(AccessEventDto dto, INotificationService notificationService)
            : base(dto.Id, dto.Timestamp)
        {
            _dto = dto;
            _notificationService = notificationService;
            CopyDetailsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteCopy);
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
        public override string Details => $"{Amount:C} • {PaymentMethod}";

        public ICommand CopyReceiptCommand { get; }

        public PaymentEventItemViewModel(SaleDto dto, INotificationService notificationService)
            : base(dto.Id, dto.Timestamp)
        {
            _dto = dto;
            _notificationService = notificationService;
            CopyReceiptCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteCopyReceipt);
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
        public override string Details => $"with {InstructorName} • {Location}";

        public ICommand ViewDetailsCommand { get; }
        public ICommand CancelBookingCommand { get; }

        public ReservationEventItemViewModel(
            ReservationDto dto,
            IReservationService service,
            IDialogService dialogService,
            Action refreshCallback)
            : base(dto.Id, dto.StartTime)
        {
            _dto = dto;
            _service = service;
            _dialogService = dialogService;
            _refreshCallback = refreshCallback;

            CancelBookingCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteCancelAsync);
            ViewDetailsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { /* Open Modal */ });
        }

        private async Task ExecuteCancelAsync()
        {
            bool confirm = await _dialogService.ShowConfirmationAsync("Cancel Booking?", "Are you sure?");
            if (confirm)
            {
                await _service.CancelReservationAsync(Id);
                _refreshCallback?.Invoke(); // Notify Parent to refresh list
            }
        }
    }
}