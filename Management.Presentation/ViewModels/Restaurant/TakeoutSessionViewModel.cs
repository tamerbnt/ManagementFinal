using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Application.DTOs;
using Management.Application.Interfaces;

using Management.Domain.Services;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class TakeoutSessionViewModel : ObservableObject
    {
        [ObservableProperty]
        private Guid _orderId;

        [ObservableProperty]
        private RestaurantOrderDto? _currentOrder;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private decimal _subtotal;

        [ObservableProperty]
        private decimal _total;

        [ObservableProperty]
        private decimal _tax;

        [ObservableProperty]
        private string _note = string.Empty;

        [ObservableProperty]
        private string _orderTypeLabel = "New Order";

        private readonly ITerminologyService _terminologyService;


        public bool IsItemSelected(string itemName) => 
            CurrentOrder?.Items.Any(i => i.Name == itemName) ?? false;

        public void RefreshTotals()
        {
            if (CurrentOrder == null)
            {
                Subtotal = 0;
                Tax = 0;
                Total = 0;
                return;
            }
            
            Subtotal = CurrentOrder.Items.Sum(i => i.Price * i.Quantity);
            Tax = 0; 
            Total = Subtotal;

            // Sync back to DTO for persistence consistency if needed
            CurrentOrder.Subtotal = Subtotal;
            CurrentOrder.Tax = 0;
            CurrentOrder.Total = Total;
            
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Tax));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(CurrentOrder));
        }

        partial void OnCurrentOrderChanged(RestaurantOrderDto? value)
        {
            RefreshTotals();
            if (value != null)
            {
                if (value.DailyOrderNumber > 0)
                {
                    DisplayName = string.Format(_terminologyService.GetTerm("Terminology.Restaurant.Order.Ticket"), value.DailyOrderNumber);
                }
                else
                {
                    DisplayName = _terminologyService.GetTerm("Terminology.Restaurant.Order.Ticket").Replace("{0}", "...");
                }

                if (string.IsNullOrEmpty(value.TableNumber) || value.TableNumber == "Takeout")
                {
                    OrderTypeLabel = string.Empty;
                }
                else
                {
                    OrderTypeLabel = string.Format(_terminologyService.GetTerm("Terminology.Restaurant.Order.TableNumber"), value.TableNumber);
                }
            }
        }

        public TakeoutSessionViewModel(Guid orderId, string displayName, ITerminologyService terminologyService)
        {
            OrderId = orderId;
            DisplayName = displayName;
            _terminologyService = terminologyService;
        }
    }
}
