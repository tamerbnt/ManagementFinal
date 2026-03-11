using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Presentation.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class OpenOrdersViewModel : FacilityAwareViewModelBase, IModalAware
    {
        private readonly IOrderService _orderService;
        private readonly ITableService _tableService;
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly IDispatcher _dispatcher;

        [ObservableProperty]
        private ObservableCollection<RestaurantOrderDto> _openOrders = new();

        [ObservableProperty]
        private bool _isLoading;

        public OpenOrdersViewModel(
            IOrderService orderService,
            ITableService tableService,
            ModalNavigationStore modalNavigationStore,
            IDispatcher dispatcher,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<OpenOrdersViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _tableService = tableService ?? throw new ArgumentNullException(nameof(tableService));
            _modalNavigationStore = modalNavigationStore ?? throw new ArgumentNullException(nameof(modalNavigationStore));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            Title = GetTerm("Terminology.Restaurant.Order.PayBill");
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            await LoadOrdersAsync();
        }

        private async Task LoadOrdersAsync()
        {
            IsLoading = true;
            try
            {
                var result = await _orderService.GetActiveOrdersAsync(CurrentFacilityId);
                if (result.IsSuccess && result.Value != null)
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        // Show orders that have been sent to the kitchen (not just pending draft),
                        // and crucially, only show floor plan tables that actually have items to pay for.
                        // Exclude takeout orders as they are handled separately or directly.
                        var validOrders = result.Value
                            .Where(o => o.Status != "Completed" && o.Status != "Cancelled" && o.Total > 0 && o.Items.Any() && o.Section != "Takeout")
                            .OrderByDescending(o => o.CreatedAt)
                            .ToList();

                        OpenOrders = new ObservableCollection<RestaurantOrderDto>(validOrders);
                    });
                }
                else if (!result.IsSuccess)
                {
                    ShowError(result.Error.Message);
                    _logger.LogWarning("Failed to load open orders: {ErrorMessage}", result.Error.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading open orders");
                ShowError("Failed to load orders. Please try again.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task PayOrderAsync(RestaurantOrderDto order)
        {
            if (order == null) return;

            await ExecuteSafeAsync(async () =>
            {
                var result = await _orderService.CompleteOrderAsync(order.Id);
                if (result.IsSuccess)
                {
                    _toastService?.ShowSuccess(string.Format(GetTerm("Terminology.Restaurant.Order.Toast.Paid"), order.TableNumber ?? "Takeout"));
                    WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(CurrentFacilityId));
                    await LoadOrdersAsync(); // Refresh list

                    // If empty, just close it
                    if (!OpenOrders.Any())
                    {
                        await _modalNavigationStore.CloseAsync();
                    }
                }
                else
                {
                    ShowError(result.Error.Message);
                }
            });
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync();
        }
    }
}
