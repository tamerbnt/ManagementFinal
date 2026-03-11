using System;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;

namespace Management.Infrastructure.Hubs
{
    /// <summary>
    /// Client-side interface for Kitchen Hub events.
    /// Implement this interface to receive real-time updates from the kitchen.
    /// </summary>
    public interface IKitchenHubClient
    {
        /// <summary>
        /// Called when an order's status changes in the kitchen.
        /// </summary>
        Task OrderStatusChanged(Guid orderId, OrderStatus newStatus, string tableNumber);
        
        /// <summary>
        /// Called when a table's status changes.
        /// </summary>
        Task TableStatusChanged(string tableNumber, TableStatus newStatus);
    }
}
