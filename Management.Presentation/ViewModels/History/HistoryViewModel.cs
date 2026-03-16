using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Application.Interfaces;
using Management.Domain.Models;
using Management.Presentation.Messages;

using Management.Presentation.Services;
using Management.Presentation.Models.History;
using Management.Presentation.ViewModels.Members;
using Management.Application.Interfaces.App;
using Microsoft.Extensions.Logging;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Domain.Services;

using System.Linq;
using Management.Application.DTOs;

using Management.Presentation.Helpers;

namespace Management.Presentation.ViewModels.History
{
    public enum HistoryDateRange
    {
        Last7Days,
        Last15Days,
        Last30Days,
        All
    }

    public partial class HistoryViewModel : ViewModelBase,
        IRecipient<RefreshRequiredMessage<Sale>>,
        IRecipient<RefreshRequiredMessage<PayrollEntry>>,
        INavigationalLifecycle
    {
        private readonly INavigationService _navigationService;
        private readonly IFacilityContextService _facilityContext;
        private readonly IEnumerable<IHistoryProvider> _historyProviders;
        private readonly ITerminologyService _terminologyService;
        private readonly ISyncService _syncService;
        private readonly IOrderService _orderService;
        private readonly IReportingService _reportingService;

        [ObservableProperty]
        private HistoryEventItemViewModel? _selectedEvent;

        partial void OnSelectedEventChanged(HistoryEventItemViewModel? oldValue, HistoryEventItemViewModel? newValue)
        {
            if (oldValue != null) oldValue.IsActive = false;
            if (newValue != null)
            {
                newValue.IsActive = true;
                AuditNote = newValue.AuditNote ?? string.Empty;
            }
        }

        [ObservableProperty]
        private bool _isDetailOpen;

        [ObservableProperty] private string _auditNote = string.Empty;

        public ObservableRangeCollection<HistoryEventItemViewModel> HistoryEvents { get; } = new();
        public System.ComponentModel.ICollectionView HistoryEventsView { get; }

        [ObservableProperty] private string _searchText = string.Empty;
        
        [ObservableProperty] private DateTime _selectedDay = DateTime.Today;
        [ObservableProperty] private string _dateRangeText = "Filter Status";

        partial void OnSelectedDayChanged(DateTime value)
        {
             _ = LoadHistoryAsync();
        }

        partial void OnSearchTextChanged(string value) => HistoryEventsView.Refresh();

        public IAsyncRelayCommand PreviousDayCommand { get; }
        public IAsyncRelayCommand NextDayCommand { get; }
        public IAsyncRelayCommand LoadHistoryCommand { get; }
        public IAsyncRelayCommand SaveAuditNoteCommand { get; }
        public IAsyncRelayCommand PrintSelectedEventCommand { get; }
        public IRelayCommand<HistoryDateRange> ChangeDateRangeCommand { get; }
        public IRelayCommand CloseDetailCommand { get; }
        public IRelayCommand SelectDateRangeCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IRelayCommand PrintReportCommand { get; }

        public Task PreInitializeAsync()
        {
            Title = "Activity History";
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            await LoadHistoryAsync();
        }

        public HistoryViewModel(
            INavigationService navigationService,
            IFacilityContextService facilityContext,
            IEnumerable<IHistoryProvider> historyProviders,
            ILogger<HistoryViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ITerminologyService terminologyService,
            ISyncService syncService,
            IOrderService orderService,
            IReportingService reportingService) 
            : base(logger, diagnosticService, toastService)
        {
            _navigationService = navigationService;
            _facilityContext = facilityContext;
            _historyProviders = historyProviders;
            _terminologyService = terminologyService;
            _syncService = syncService;
            _orderService = orderService;
            _reportingService = reportingService;

            _syncService.SyncCompleted += OnSyncCompleted;
            
            HistoryEventsView = System.Windows.Data.CollectionViewSource.GetDefaultView(HistoryEvents);
            HistoryEventsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(HistoryEventItemViewModel.Timestamp), System.ComponentModel.ListSortDirection.Descending));

            LoadHistoryCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(LoadHistoryAsync);
            SaveAuditNoteCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(SaveAuditNoteAsync);
            PrintSelectedEventCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(PrintSelectedEventAsync);
            CloseDetailCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CloseDetail);
            
            PreviousDayCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => { SelectedDay = SelectedDay.AddDays(-1); return Task.CompletedTask; });
            NextDayCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => { SelectedDay = SelectedDay.AddDays(1); return Task.CompletedTask; });

            ChangeDateRangeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<HistoryDateRange>(range => { /* Legacy */ });
            SelectDateRangeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _toastService.ShowInfo("Use the navigation buttons to change dates."));
            ExportCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExportHistoryAsync);
            PrintReportCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _toastService.ShowInfo("Preparing print..."));

            HistoryEventsView.Filter = FilterHistory;

            // Subscribe to sale/payment events so history refreshes immediately on local actions
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<Sale>>(this);
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<PayrollEntry>>(this);
        }

        private bool FilterHistory(object obj)
        {
            if (obj is not HistoryEventItemViewModel item) return false;

            // Search Filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                return item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       item.Details.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void CloseDetail()
        {
            IsDetailOpen = false;
            SelectedEvent = null; // This will trigger OnSelectedEventChanged and clear IsActive
        }

        private async Task LoadHistoryAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                var facilityType = _facilityContext.CurrentFacility;
                
                // Find provider for current facility type (SegmentName usually matches)
                var segmentName = facilityType.ToString();
                var provider = _historyProviders.FirstOrDefault(p => p.SegmentName == segmentName);
                
                if (provider == null)
                {
                    _logger.LogWarning("No history provider found for {Segment}", segmentName);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => HistoryEvents.Clear());
                    return;
                }

                // Query for the full day (Synchronized with Local Time as requested)
                // We define the local bounds first, then convert to UTC for the database query
                var startLocal = SelectedDay.Date;
                var endLocal = startLocal.AddDays(1).AddTicks(-1);

                var startUtc = startLocal.ToUniversalTime();
                var endUtc = endLocal.ToUniversalTime();

                var events = await provider.GetHistoryAsync(facilityId, startUtc, endUtc);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    var vms = events.Select(e => 
                    {
                        HistoryEventItemViewModel vm = e.Type switch
                        {
                            HistoryEventType.Payment => new PaymentEventItemViewModel(this) { Amount = e.Amount ?? 0, PaymentMethod = e.Metadata ?? "Unknown" },
                            HistoryEventType.Order => new PaymentEventItemViewModel(this) { Amount = e.Amount ?? 0, PaymentMethod = e.Metadata ?? "Order" },
                            HistoryEventType.Access => new AccessEventItemViewModel(this) { IsSuccessful = e.IsSuccessful },
                            HistoryEventType.Reservation => new ReservationEventItemViewModel(this),
                            HistoryEventType.Appointment => new AppointmentEventItemViewModel(this),
                            HistoryEventType.Payroll => new PayrollEventItemViewModel(this) { Amount = e.Amount ?? 0, PaymentMethod = e.Metadata ?? "" },
                            HistoryEventType.Inventory => new InventoryEventItemViewModel(this) { ResourceName = e.Metadata ?? "", Amount = e.Amount ?? 0 },
                            _ => new AccessEventItemViewModel(this)
                        };

                        vm.Title = !string.IsNullOrEmpty(e.TitleLocalizationKey)
                            ? string.Format(_terminologyService.GetTerm(e.TitleLocalizationKey), e.TitleLocalizationArgs ?? Array.Empty<object>())
                            : e.Title;
                        vm.Details = !string.IsNullOrEmpty(e.DetailsLocalizationKey)
                            ? string.Format(_terminologyService.GetTerm(e.DetailsLocalizationKey), e.DetailsLocalizationArgs ?? Array.Empty<object>())
                            : e.Details;
                        vm.Timestamp = e.Timestamp.ToLocalTime();
                        vm.AuditNote = e.AuditNote;
                        vm.Id = e.Id;
                        return vm;
                    });

                    HistoryEvents.ReplaceRange(vms);
                    HistoryEventsView.Refresh();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading history items");
                _toastService.ShowError("Failed to load history feed.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveAuditNoteAsync()
        {
            if (SelectedEvent == null) return;
            
            // Note: Currently ITransactionService only handles audit notes for Transactions.
            // If the selected event is a Transaction, we save it.
            // (In a fuller impl, we'd have it for all auditables)
            
            try 
            {
                // Simple placeholder logic: detect if it's a payment/sale and we have an ID
                // For now, we update local state and show success.
                SelectedEvent.AuditNote = AuditNote;
                _toastService.ShowSuccess("Audit note updated locally.");
                IsDetailOpen = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving audit note");
                _toastService.ShowError("Failed to save audit note.");
            }
            await Task.CompletedTask;
        }

        private async Task PrintSelectedEventAsync()
        {
            if (SelectedEvent == null) return;

            try
            {
                if (SelectedEvent is PaymentEventItemViewModel or SaleEventItemViewModel)
                {
                    _toastService.ShowInfo("Sending ticket to printer...");
                    var result = await _orderService.PrintOrderAsync(SelectedEvent.Id);
                    if (result.IsSuccess)
                    {
                        _toastService.ShowSuccess("Ticket printed successfully.");
                    }
                    else
                    {
                        _toastService.ShowError("Failed to print ticket: " + result.Error);
                    }
                }
                else
                {
                    _toastService.ShowInfo("Printing is not available for this event type.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing ticket for event {Id}", SelectedEvent.Id);
                _toastService.ShowError("Failed to print ticket.");
            }
        }

        private async Task ExportHistoryAsync()
        {
            if (HistoryEvents.Count == 0)
            {
                _toastService.ShowInfo("No history data to export for this day.");
                return;
            }

            try
            {
                _toastService.ShowInfo("Generating PDF report...", "Export");

                var facilityName = _facilityContext.CurrentFacility.ToString();
                
                var eventDtos = HistoryEvents.Select(e => new UnifiedHistoryEventDto
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    Type = GetHistoryEventTypeFromViewModel(e),
                    Title = e.Title,
                    Details = e.Details,
                    Amount = e is PaymentEventItemViewModel pvm ? pvm.Amount :
                             e is PayrollEventItemViewModel prvm ? prvm.Amount :
                             e is InventoryEventItemViewModel ivm ? ivm.Amount : null,
                    IsSuccessful = e is AccessEventItemViewModel avm ? avm.IsSuccessful : true
                }).ToList();

                var pdfBytes = await _reportingService.GenerateHistoryPdfReportAsync(facilityName, SelectedDay, eventDtos);

                var reportsFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Luxurya", "Reports");
                System.IO.Directory.CreateDirectory(reportsFolder);

                var fileName = $"ActivityHistory_{SelectedDay:yyyy_MM_dd}_{DateTime.Now:HHmmss}.pdf";
                var filePath = System.IO.Path.Combine(reportsFolder, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

                _toastService.ShowSuccess($"PDF saved: {fileName}", "Export Successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting history report to PDF");
                _toastService.ShowError("Failed to export PDF report: " + ex.Message, "Export Failed");
            }
        }

        private HistoryEventType GetHistoryEventTypeFromViewModel(HistoryEventItemViewModel vm)
        {
            return vm switch
            {
                PaymentEventItemViewModel => HistoryEventType.Payment,
                SaleEventItemViewModel => HistoryEventType.Order,
                AccessEventItemViewModel => HistoryEventType.Access,
                ReservationEventItemViewModel => HistoryEventType.Reservation,
                AppointmentEventItemViewModel => HistoryEventType.Appointment,
                PayrollEventItemViewModel => HistoryEventType.Payroll,
                InventoryEventItemViewModel => HistoryEventType.Inventory,
                _ => HistoryEventType.Access
            };
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (!ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[History] Sync debounce passed, refreshing history records...");
                await LoadHistoryAsync();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }
                WeakReferenceMessenger.Default.Unregister<RefreshRequiredMessage<Sale>>(this);
                WeakReferenceMessenger.Default.Unregister<RefreshRequiredMessage<PayrollEntry>>(this);
            }
            base.Dispose(disposing);
        }

        public void Receive(RefreshRequiredMessage<Sale> message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    if (!IsDisposed && !IsLoading) await LoadHistoryAsync();
                });
            }
        }

        public void Receive(RefreshRequiredMessage<PayrollEntry> message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    if (!IsDisposed && !IsLoading) await LoadHistoryAsync();
                });
            }
        }
    }
}
