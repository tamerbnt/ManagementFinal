using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Services.State;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;
using Management.Application.DTOs;

namespace Management.Presentation.ViewModels.Restaurant
{
    /// <summary>Log a new purchase entry for a specific inventory resource.</summary>
    public partial class LogPurchaseViewModel : ObservableObject
    {
        private readonly IInventoryService _inventoryService;
        private readonly IFacilityContextService _facilityContext;
        private readonly SessionManager _sessionManager;
        private Guid _resourceId;

        [ObservableProperty]
        private bool _isNewResource;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _resourceName;

        [ObservableProperty]
        private string _unit;

        public static readonly string[] AvailableUnits = { "kg", "g", "L", "mL", "piece", "pack", "box", "bottle", "bag", "can" };
        public string[] AvailableUnitsList => AvailableUnits;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _quantity = string.Empty;

        [ObservableProperty]
        private DateTime _purchaseDate = DateTime.Today;

        [ObservableProperty]
        private string _note = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private string _unitPrice = string.Empty;

        [ObservableProperty]
        private string _totalPrice = string.Empty;

        private bool _isCalculating;

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public event EventHandler? Saved;
        public event EventHandler? Canceled;

        public LogPurchaseViewModel(
            Guid? resourceId, // Nullable to indicate a new resource
            string? resourceName,
            string? unit,
            IInventoryService inventoryService,
            IFacilityContextService facilityContext,
            SessionManager sessionManager)
        {
            _inventoryService = inventoryService;
            _facilityContext = facilityContext;
            _sessionManager = sessionManager;

            if (resourceId.HasValue)
            {
                _isNewResource = false;
                _resourceId = resourceId.Value;
                _resourceName = resourceName ?? string.Empty;
                _unit = unit ?? "kg";
            }
            else
            {
                _isNewResource = true;
                _resourceId = Guid.Empty;
                _resourceName = string.Empty;
                _unit = "kg";
            }

            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new RelayCommand(() => Canceled?.Invoke(this, EventArgs.Empty));
        }

        private bool CanSave()
        {
            bool hasValidQuantity = decimal.TryParse(Quantity, out var q) && q > 0;
            if (IsNewResource)
            {
                return hasValidQuantity && !string.IsNullOrWhiteSpace(ResourceName);
            }
            return hasValidQuantity;
        }

        partial void OnResourceNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnQuantityChanged(string value) => RecalculateTotal();
        partial void OnUnitPriceChanged(string value) => RecalculateTotal();
        partial void OnTotalPriceChanged(string value) => RecalculateUnit();

        private void RecalculateTotal()
        {
            if (_isCalculating) return;
            _isCalculating = true;
            try
            {
                if (decimal.TryParse(Quantity, out var q) && decimal.TryParse(UnitPrice, out var p))
                {
                    TotalPrice = (q * p).ToString("0.##");
                }
            }
            finally { _isCalculating = false; }
        }

        private void RecalculateUnit()
        {
            if (_isCalculating) return;
            _isCalculating = true;
            try
            {
                if (decimal.TryParse(Quantity, out var q) && decimal.TryParse(TotalPrice, out var t) && q > 0)
                {
                    UnitPrice = (t / q).ToString("0.##");
                }
            }
            finally { _isCalculating = false; }
        }

        private async Task SaveAsync()
        {
            IsLoading = true;
            StatusMessage = "Processing purchase log...";
            try
            {
                if (!decimal.TryParse(Quantity, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                {
                    StatusMessage = "Invalid quantity provided.";
                    return;
                }

                // If it's a new resource, create it first
                if (IsNewResource)
                {
                    var newResourceDto = new InventoryResourceDto
                    {
                        Id = Guid.NewGuid(),
                        Name = ResourceName.Trim(),
                        Unit = Unit,
                        FacilityId = _facilityContext.CurrentFacilityId,
                        TenantId = _sessionManager.CurrentTenantId
                    };

                    var resourceSuccess = await _inventoryService.AddResourceAsync(newResourceDto);
                    if (!resourceSuccess)
                    {
                        StatusMessage = "Failed to create new resource.";
                        return;
                    }

                    // Assign the newly generated ID for the subsequent purchase log
                    _resourceId = newResourceDto.Id;
                }

                var purchaseDto = new InventoryPurchaseDto
                {
                    Id = Guid.NewGuid(),
                    ResourceId = _resourceId,
                    Quantity = qty,
                    Unit = Unit,
                    Date = PurchaseDate,
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note,
                    FacilityId = _facilityContext.CurrentFacilityId,
                    TenantId = _sessionManager.CurrentTenantId
                };

                // Add Price
                if (decimal.TryParse(TotalPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var totalCost))
                {
                    purchaseDto.TotalPrice = totalCost;
                }
                
                if (decimal.TryParse(UnitPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var unitPrice))
                {
                    purchaseDto.UnitPrice = unitPrice;
                }

                var success = await _inventoryService.LogPurchaseAsync(purchaseDto);
                if (success)
                {
                    WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<InventoryPurchaseDto>(_facilityContext.CurrentFacilityId));
                    Saved?.Invoke(this, EventArgs.Empty);
                }
                else
                    StatusMessage = "Failed to log purchase. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
