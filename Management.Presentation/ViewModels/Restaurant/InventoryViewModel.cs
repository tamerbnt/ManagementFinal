using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Services.State;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Application.Interfaces.App;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Restaurant
{
    /// <summary>
    /// Drives the full-screen Inventory view. Manages resource cards, purchase history,
    /// and slide-in editors (LogPurchase + AddResource).
    /// </summary>
    public partial class InventoryViewModel : FacilityAwareViewModelBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly SessionManager _sessionManager;
        private readonly ILogger<InventoryViewModel> _logger;
        private readonly ISyncService _syncService;

        [ObservableProperty]
        private ObservableCollection<InventoryResourceCardViewModel> _resources = new();

        [ObservableProperty]
        private InventoryResourceCardViewModel? _selectedItem;

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private string? _selectedCategoryFilter;

        [ObservableProperty]
        private ObservableCollection<InventoryPurchaseViewModel> _purchaseHistory = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _statusMessage;

        // Drawer state
        [ObservableProperty]
        private bool _isDrawerOpen;

        [ObservableProperty]
        private object? _currentDrawerContent;

        // Commands
        public ICommand CloseDrawerCommand { get; }
        public IAsyncRelayCommand LoadDataCommand { get; }
        public IRelayCommand GoBackCommand { get; }
        public IRelayCommand AddResourceCommand { get; }

        // Event raised to tell MenuManagementViewModel to hide the Inventory screen
        public event EventHandler? NavigatedBack;

        public InventoryViewModel(
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IInventoryService inventoryService,
            IFacilityContextService facilityContext,
            SessionManager sessionManager,
            ILogger<InventoryViewModel> logger,
            ISyncService syncService) : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _inventoryService = inventoryService;
            _sessionManager = sessionManager;
            _logger = logger;
            _syncService = syncService;

            _syncService.SyncCompleted += OnSyncCompleted;

            Title = GetTerm("Strings.Restaurant.Inventory");

            CloseDrawerCommand = new RelayCommand(CloseDrawer);
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            GoBackCommand = new RelayCommand(() => NavigatedBack?.Invoke(this, EventArgs.Empty));
            AddResourceCommand = new RelayCommand(ShowAddResource);
        }

        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            Title = GetTerm("Strings.Restaurant.Inventory");
        }

        public IEnumerable<InventoryResourceCardViewModel> FilteredResources
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SelectedCategoryFilter) || SelectedCategoryFilter == "All")
                    return Resources;

                return Resources.Where(r => r.Category == SelectedCategoryFilter);
            }
        }

        public async Task InitializeAsync()
        {
            await LoadDataAsync();
        }

        private void CloseDrawer()
        {
            IsDrawerOpen = false;
            if (SelectedItem != null) SelectedItem.IsActive = false;
            CurrentDrawerContent = null;
            // FIX: Force clear the selection so the same card can be re-clicked immediately
            SelectedItem = null;
        }

        partial void OnSelectedItemChanged(InventoryResourceCardViewModel? value)
        {
            foreach (var r in Resources)
            {
                r.IsActive = (r == value);
            }

            if (value != null)
            {
                ShowHistory(value);
            }
        }

        private void ShowHistory(InventoryResourceCardViewModel resource)
        {
            CurrentDrawerContent = resource;
            IsDrawerOpen = true;
        }

        partial void OnSelectedCategoryFilterChanged(string? value) => OnPropertyChanged(nameof(FilteredResources));

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;
            try
            {
                var facilityId = _facilityContext.CurrentFacilityId;

                var resources = await _inventoryService.GetResourcesAsync(facilityId);
                Resources = new ObservableCollection<InventoryResourceCardViewModel>();
                foreach (var r in resources)
                {
                    var card = new InventoryResourceCardViewModel(r);
                    card.LogPurchaseRequested += OnLogPurchaseRequested;
                    Resources.Add(card);
                }

                // Update categories
                var cats = Resources.Select(r => r.Category).Distinct().OrderBy(c => c).ToList();
                Categories = new ObservableCollection<string> { "All" };
                foreach (var c in cats) Categories.Add(c);
                SelectedCategoryFilter = "All";

                OnPropertyChanged(nameof(FilteredResources));
                StatusMessage = string.Empty;

                var history = await _inventoryService.GetPurchaseHistoryAsync(facilityId);
                PurchaseHistory = new ObservableCollection<InventoryPurchaseViewModel>();
                foreach (var h in history)
                    PurchaseHistory.Add(new InventoryPurchaseViewModel(h));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load inventory data");
                StatusMessage = "Failed to load inventory data.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnLogPurchaseRequested(object? sender, InventoryResourceCardViewModel card)
        {
            var logVm = new LogPurchaseViewModel(card.ResourceId, card.Name, card.Unit,
                _inventoryService, _facilityContext, _sessionManager);
            logVm.Saved += async (_, _) =>
            {
                IsDrawerOpen = false;
                CurrentDrawerContent = null;
                await LoadDataAsync();
            };
            logVm.Canceled += (_, _) =>
            {
                IsDrawerOpen = false;
                CurrentDrawerContent = null;
            };
            CurrentDrawerContent = logVm;
            IsDrawerOpen = true;
        }

        private void ShowAddResource()
        {
            var vm = new LogPurchaseViewModel(
                null, // null ID triggers "New Resource" mode
                null,
                null,
                _inventoryService,
                _facilityContext,
                _sessionManager);

            vm.Saved += (s, e) =>
            {
                IsDrawerOpen = false;
                _ = LoadDataAsync();
            };

            vm.Canceled += (s, e) => IsDrawerOpen = false;

            CurrentDrawerContent = vm;
            IsDrawerOpen = true;
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            // InventoryViewModel is a sub-screen embedded in MenuManagementViewModel.
            // We skip the IsActive check and rely solely on the shared 3-second debounce
            // plus IsLoading to prevent redundant refreshes.
            if (IsDisposed || IsLoading) return;
            var now = DateTime.UtcNow;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[Inventory] Sync debounce passed, refreshing resource data...");
                await LoadDataAsync();
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
            }
            base.Dispose(disposing);
        }
    }

    // â”€â”€â”€ Resource Card â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public partial class InventoryResourceCardViewModel : ObservableObject
    {
        private readonly InventoryResourceDto _dto;

        public Guid ResourceId => _dto.Id;

        [ObservableProperty] private string _name;
        [ObservableProperty] private string _unit;
        [ObservableProperty] private decimal _cumulativeTotal;
        [ObservableProperty] private bool _isActive;

        public string Category => "Supply"; // Default static category to match Menu filter pills UI

        public string TotalDisplay => $"{CumulativeTotal:0.##} {Unit}";

        public IRelayCommand LogPurchaseCommand { get; }
        public event EventHandler<InventoryResourceCardViewModel>? LogPurchaseRequested;

        public InventoryResourceCardViewModel(InventoryResourceDto dto)
        {
            _dto = dto;
            _name = dto.Name;
            _unit = dto.Unit;
            _cumulativeTotal = dto.CumulativeTotal;
            LogPurchaseCommand = new RelayCommand(() =>
                LogPurchaseRequested?.Invoke(this, this));
        }
    }

    // â”€â”€â”€ Purchase History Row â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class InventoryPurchaseViewModel
    {
        public string ResourceName { get; }
        public string QuantityDisplay { get; }
        public string DateDisplay { get; }
        public string? Note { get; }

        public InventoryPurchaseViewModel(InventoryPurchaseDto dto)
        {
            ResourceName = dto.ResourceName;
            QuantityDisplay = $"{dto.Quantity:0.##} {dto.Unit}";
            DateDisplay = dto.Date.ToString("MMM dd, yyyy");
            Note = dto.Note;
        }
    }
}
