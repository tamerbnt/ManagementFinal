using System;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services
{
    public interface IOrderService
    {
        Task<RestaurantOrder> CreateOrderAsync(string tableNumber, int partySize);
        Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus);
        Task<RestaurantOrder?> GetActiveOrderForTableAsync(string tableNumber);
        Task<bool> OccupyTableAsync(string tableNumber, int partySize);
    }

    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;

        public OrderService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> OccupyTableAsync(string tableNumber, int partySize)
        {
            // CONCURRENCY CHECK: Prevent Ghost Orders
            var table = await _context.RestaurantTables
                .FirstOrDefaultAsync(t => t.TableNumber.ToString() == tableNumber);

            if (table == null)
                throw new InvalidOperationException($"Table {tableNumber} does not exist.");

            // CRITICAL: Check current status
            if (table.Status != TableStatus.Available)
            {
                return false; // Table already occupied
            }

            // Check for existing active order (double-check)
            var existingOrder = await GetActiveOrderForTableAsync(tableNumber);
            if (existingOrder != null)
            {
                return false; // Ghost order would be created
            }

            // Atomic operation: Create order + Update table
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new RestaurantOrder
                {
                    TableNumber = table.TableNumber.ToString(),
                    Status = OrderStatus.Pending
                };

                table.Status = TableStatus.Occupied;
                table.CurrentOccupancy = partySize;

                _context.RestaurantOrders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<RestaurantOrder> CreateOrderAsync(string tableNumber, int partySize)
        {
            var success = await OccupyTableAsync(tableNumber, partySize);
            if (!success)
                throw new InvalidOperationException($"Cannot create order for table {tableNumber}. Table is not available.");

            return await _context.RestaurantOrders
                .FirstOrDefaultAsync(o => o.TableNumber == tableNumber && o.Status == OrderStatus.Pending)
                ?? throw new InvalidOperationException("Order creation failed.");
        }

        public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus)
        {
            var order = await _context.RestaurantOrders.FindAsync(orderId);
            if (order == null) return false;

            // STATE MACHINE VALIDATION: Prevent Jumps
            if (!IsValidTransition(order.Status, newStatus))
            {
                throw new InvalidOperationException(
                    $"Invalid state transition: {order.Status} → {newStatus}. " +
                    "Valid flow: Pending → InKitchen → Ready → Delivered → Paid");
            }

            order.Status = newStatus;

            if (newStatus == OrderStatus.Delivered)
                order.DeliveredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<RestaurantOrder?> GetActiveOrderForTableAsync(string tableNumber)
        {
            return await _context.RestaurantOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => 
                    o.TableNumber == tableNumber && 
                    o.Status != OrderStatus.Paid && 
                    o.Status != OrderStatus.Cancelled);
        }

        private bool IsValidTransition(OrderStatus current, OrderStatus next)
        {
            return (current, next) switch
            {
                // Forward transitions
                (OrderStatus.Pending, OrderStatus.InKitchen) => true,
                (OrderStatus.InKitchen, OrderStatus.Ready) => true,
                (OrderStatus.Ready, OrderStatus.Delivered) => true,
                (OrderStatus.Delivered, OrderStatus.Paid) => true,
                
                // Cancellation allowed from any state except Paid
                (_, OrderStatus.Cancelled) when current != OrderStatus.Paid => true,
                
                // Idempotent (same state)
                _ when current == next => true,
                
                // All other transitions BLOCKED
                _ => false
            };
        }
    }
}
