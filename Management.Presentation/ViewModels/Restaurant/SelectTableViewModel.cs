using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Presentation.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Management.Domain.Models.Restaurant;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;
using Management.Presentation.Services;
using Management.Presentation.Services.State;
using Management.Presentation.Services.Localization;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class SelectTableViewModel : FacilityAwareViewModelBase
    {
        private readonly ITableService _tableService;
        private readonly IOrderService _orderService;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly INavigationService _navigationService;
        private readonly SessionManager _sessionManager;
        private readonly IDispatcher _dispatcher;

        [ObservableProperty]
        private ObservableCollection<TableModel> _availableTables = new();

        [ObservableProperty]
        private ObservableCollection<TableModel> _filteredTables = new();

        [ObservableProperty]
        private ObservableCollection<string> _sections = new();

        [ObservableProperty]
        private string _selectedSection = "All";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartOrderCommand))]
        private TableModel? _selectedTable;

        public SelectTableViewModel(
            ITableService tableService,
            IOrderService orderService,
            ModalNavigationStore modalNavigationStore,
            INavigationService navigationService,
            SessionManager sessionManager,
            IDispatcher dispatcher,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<SelectTableViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ILocalizationService? localizationService = null)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _tableService = tableService ?? throw new ArgumentNullException(nameof(tableService));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _modalNavigationStore = modalNavigationStore ?? throw new ArgumentNullException(nameof(modalNavigationStore));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            Title = GetTerm("Terminology.Restaurant.Order.SelectTable");
        }

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Terminology.Restaurant.Order.SelectTable");
        }

        public override async Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default)
        {
            await LoadTablesAsync();
        }

        private async Task LoadTablesAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                _logger?.LogInformation("Loading tables for facility {FacilityId}", CurrentFacilityId);
                var tables = await _tableService.GetTablesAsync(CurrentFacilityId);
                var available = tables.Where(t => t.Status == TableStatus.Available).ToList();
                
                _logger?.LogInformation("Found {AvailableCount} available tables", available.Count);

                await _dispatcher.InvokeAsync(() =>
                {
                    AvailableTables = new ObservableCollection<TableModel>(available);
                    
                    // Extract unique sections and add "All"
                    var uniqueSections = available
                        .Select(t => t.Section)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList();

                    Sections.Clear();
                    Sections.Add("All");
                    foreach (var section in uniqueSections)
                    {
                        Sections.Add(section);
                    }

                    SelectedSection = "All";
                    ApplyFilter();
                });
            });
        }

        partial void OnSelectedSectionChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (SelectedSection == "All")
            {
                FilteredTables = new ObservableCollection<TableModel>(AvailableTables);
            }
            else
            {
                var filtered = AvailableTables.Where(t => t.Section == SelectedSection).ToList();
                FilteredTables = new ObservableCollection<TableModel>(filtered);
            }
            
            // Clear selection if it's no longer in the filtered list
            if (SelectedTable != null && !FilteredTables.Contains(SelectedTable))
            {
                SelectedTable = null;
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartOrder))]
        private async Task StartOrderAsync()
        {
            if (SelectedTable == null) return;

            await ExecuteSafeAsync(async () =>
            {
                var result = await _orderService.StartOrderAsync(
                    SelectedTable.Id,
                    SelectedTable.Label,
                    _sessionManager.CurrentTenantId, 
                    CurrentFacilityId);

                if (result.IsSuccess)
                {
                    // Update table status (local sync)
                    SelectedTable.Status = TableStatus.Occupied;
                    await _tableService.UpdateTableAsync(SelectedTable);

                    // Navigate to POS with the new order ID
                    await _modalNavigationStore.CloseAsync();
                    WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(CurrentFacilityId));
                    await _navigationService.NavigateToAsync<RestaurantOrderingViewModel>(result.Value);
                }
                else
                {
                    ShowError(result.Error.Message);
                }
            });
        }

        private bool CanStartOrder() => SelectedTable != null;

        [RelayCommand]
        public async Task Cancel()
        {
            await _modalNavigationStore.CloseAsync();
        }
    }
}
