using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;

namespace Management.Presentation.Services.Restaurant
{
    public interface IOrderService
    {
        ObservableCollection<RestaurantOrder> ActiveOrders { get; }
        Task<RestaurantOrder> CreateOrderAsync(Guid facilityId, string tableNumber);
        Task UpdateOrderStatusAsync(Guid facilityId, Guid orderId, OrderStatus nextStatus);
        Task AddItemToOrderAsync(Guid facilityId, Guid orderId, RestaurantMenuItem item);
        decimal CalculateTax(decimal subtotal);
    }

    public class OrderService : IOrderService
    {
        public ObservableCollection<RestaurantOrder> ActiveOrders { get; } = new();

        public async Task<RestaurantOrder> CreateOrderAsync(Guid facilityId, string tableNumber)
        {
            await Task.Delay(10); // Simulate network
            var order = new RestaurantOrder
            {
                TableNumber = tableNumber,
                Status = OrderStatus.Pending
            };
            ActiveOrders.Add(order);
            return order;
        }

        public async Task UpdateOrderStatusAsync(Guid facilityId, Guid orderId, OrderStatus nextStatus)
        {
            await Task.Delay(10);
            var order = ActiveOrders.FirstOrDefault(o => o.Id == orderId);
            if (order != null)
            {
                order.Status = nextStatus;
                if (nextStatus == OrderStatus.Delivered) order.DeliveredAt = DateTime.Now;
                
                // If completed, move to history (simulated)
                if (nextStatus == OrderStatus.Completed)
                {
                    ActiveOrders.Remove(order);
                }
            }
        }

        public async Task AddItemToOrderAsync(Guid facilityId, Guid orderId, RestaurantMenuItem menuItem)
        {
            await Task.Delay(10);
            var order = ActiveOrders.FirstOrDefault(o => o.Id == orderId);
            if (order == null) return;

            var existing = order.Items.FirstOrDefault(i => i.Name == menuItem.Name);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                order.Items.Add(new OrderItem { Name = menuItem.Name, Price = menuItem.Price, Quantity = 1 });
            }

            RecalculateTotals(order);
        }

        public decimal CalculateTax(decimal subtotal)
        {
            return Math.Round(subtotal * 0.15m, 2); // 15% Tax as per Restaurant Mock
        }

        private void RecalculateTotals(RestaurantOrder order)
        {
            order.Subtotal = order.Items.Sum(i => i.Total);
            order.Tax = CalculateTax(order.Subtotal);
        }
    }
}
