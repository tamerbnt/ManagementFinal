using System.Collections.Generic;
using System.Linq;
using Management.Domain.Primitives;

namespace Management.Domain.Models.Restaurant
{
    public enum TableStatus
    {
        Available,
        Occupied,
        Cleaning
    }

    public class TableModel : Entity
    {

        public string Number { get; set; } = string.Empty;
        public int MaxSeats { get; set; }
        public int CurrentOccupancy { get; set; }
        public TableStatus Status { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public bool CanChangeStatus(TableStatus nextStatus)
        {
            if (nextStatus == TableStatus.Occupied) return Status == TableStatus.Available;
            if (nextStatus == TableStatus.Cleaning) return Status == TableStatus.Occupied;
            if (nextStatus == TableStatus.Available) return Status == TableStatus.Cleaning;
            return true;
        }
    }

    public enum OrderStatus
    {
        Pending,
        InProgress,
        Ready,
        Delivered,
        Completed
    }

    public class RestaurantOrder : Entity
    {

        public string TableNumber { get; set; } = string.Empty;
        public DateTime? DeliveredAt { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total => Subtotal + Tax;

        public void CalculateTotal(decimal taxRate = 0.15m)
        {
            Subtotal = Items.Sum(i => i.Price * i.Quantity);
            Tax = Math.Round(Subtotal * taxRate, 2);
            // Total is calculated property
        }
    }

    public class OrderItem : Entity
    {
        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Total => Price * Quantity;
    }

    public class RestaurantMenuItem : Entity
    {
        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public string[] Ingredients { get; set; } = Array.Empty<string>();
    }
}
