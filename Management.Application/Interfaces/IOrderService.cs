using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Common;

namespace Management.Application.Interfaces
{
    public interface IOrderService
    {
        Task<Result<Guid>> StartOrderAsync(Guid? tableId, string? tableNumber, Guid tenantId, Guid facilityId, int partySize = 1);
        Task<Result> AddItemToOrderAsync(Guid orderId, string itemName, decimal price, int quantity);
        Task<Result> UpdateItemQuantityAsync(Guid orderId, string itemName, int newQuantity);
        Task<Result> RemoveItemFromOrderAsync(Guid orderId, string itemName);
        Task<Result> SendToKitchenAsync(Guid orderId);
        Task<Result> CompleteOrderAsync(Guid orderId);
        Task<Result> CancelOrderAsync(Guid orderId);
        
        Task<Result<IEnumerable<RestaurantOrderDto>>> GetActiveOrdersAsync(Guid? facilityId = null);
        Task<Result<RestaurantOrderDto?>> GetOrderByTableAsync(string tableNumber);
        Task<Result<RestaurantOrderDto?>> GetOrderByTableIdAsync(Guid tableId);
        Task<Result<RestaurantOrderDto?>> GetOrderByIdAsync(Guid orderId);
        Task<Result> PrintOrderAsync(Guid orderId);
        Task<Result<decimal>> GetTodayRevenueAsync(Guid facilityId, DateTime startDate, DateTime endDate);
        Task<Result<IEnumerable<RestaurantOrderDto>>> GetRecentActivitiesAsync(Guid facilityId, int count);
        Task<Result<IEnumerable<RestaurantOrderDto>>> GetOrdersByRangeAsync(Guid facilityId, DateTime start, DateTime end);
    }

    public class RestaurantOrderDto
    {
        public Guid Id { get; set; }
        public Guid? TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;   // e.g. "Main Hall", "Patio", "Bar Seating"
        public string Status { get; set; } = string.Empty;
        public int DailyOrderNumber { get; set; }
        public int PartySize { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public List<OrderItemDto> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class OrderItemDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Total => Price * Quantity;
    }
}
