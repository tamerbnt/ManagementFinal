using System.Diagnostics;
using System.Threading.Tasks;
using Management.Application.Interfaces;
using Management.Domain.Models;
using Management.Domain.Models.Restaurant;

namespace Management.Infrastructure.Services
{
    public class MockPrinterService : IPrinterService
    {
        public Task PrintTransactionAsync(Transaction transaction)
        {
            Debug.WriteLine($"[MockPrinter] Printing Transaction {transaction.Id}");
            Debug.WriteLine($"[MockPrinter] Date: {transaction.Timestamp}");
            Debug.WriteLine("------------------------------------------");
            foreach (var item in transaction.Items)
            {
                Debug.WriteLine($"{item.ProductName} x{item.Quantity} @ {item.Price:C}");
            }
            Debug.WriteLine("------------------------------------------");
            Debug.WriteLine($"[MockPrinter] TOTAL: {transaction.TotalAmount:C}");
            return Task.CompletedTask;
        }

        public Task OpenCashDrawerAsync()
        {
            Debug.WriteLine("[Hardware] KACHINK! Cash Drawer Opened.");
            return Task.CompletedTask;
        }

        public Task PrintOrderAsync(RestaurantOrder order)
        {
             Debug.WriteLine($"[MockPrinter] Printing Order Ticket #{order.DailyOrderNumber}");
             Debug.WriteLine($"[MockPrinter] Table: {order.TableNumber}");
             Debug.WriteLine($"[MockPrinter] Date: {order.CreatedAt:yyyy-MM-dd HH:mm:ss}");
             Debug.WriteLine("------------------------------------------");
             foreach (var item in order.Items)
             {
                 Debug.WriteLine($"{item.Name} x{item.Quantity}");
             }
             Debug.WriteLine("------------------------------------------");
             Debug.WriteLine("[MockPrinter] Wait for your number to be called.");
             return Task.CompletedTask;
        }
    }
}
