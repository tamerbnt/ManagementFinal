using System.Collections.Generic;
using System.Linq;
using Management.Domain.Primitives;

namespace Management.Domain.Models.Restaurant
{
    public enum TableStatus
    {
        Available,
        Occupied,
        Cleaning,
        Ready,
        OrderSent,
        BillRequested,
        Dirty
    }

    public class TableModel : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public int TableNumber { get; set; }
        public string Label { get; set; } = string.Empty;   // Display label e.g. "T1", "VIP-1"
        public string Section { get; set; } = "Main Hall";  // "Main Hall" | "Patio" | "Bar Seating"
        public int MaxSeats { get; set; }
        public int CurrentOccupancy { get; set; }
        public TableStatus Status { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 100;
        public double Height { get; set; } = 100;
        public string Shape { get; set; } = "Square";

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
        Completed,
        Paid,
        Cancelled,
        InKitchen
    }

    public class RestaurantOrder : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public Guid? TableId { get; set; }
        public string? TableNumber { get; set; }
        public string? Section { get; set; }   // e.g. "Main Hall", "Patio", "Bar Seating"
        public int DailyOrderNumber { get; set; }
        public int PartySize { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total => Subtotal + Tax;

        public void CalculateTotal(decimal taxRate = 0m)
        {
            Subtotal = Items.Sum(i => i.Price * i.Quantity);
            Tax = 0;
            // Total is calculated property (Subtotal + Tax)
        }
    }

    public class OrderItem : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public Guid RestaurantOrderId { get; set; }
        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Total => Price * Quantity;
    }

    public class RestaurantMenuItem : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public string[] Ingredients { get; set; } = Array.Empty<string>();
    }
}
