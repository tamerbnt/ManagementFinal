using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;

namespace Management.Domain.Interfaces
{
    public interface IOrderRepository : IRepository<RestaurantOrder>
    {
        Task<RestaurantOrder?> GetActiveOrderByTableAsync(string tableNumber);
        Task<RestaurantOrder?> GetActiveOrderByTableIdAsync(Guid tableId);
        Task<IEnumerable<RestaurantOrder>> GetActiveOrdersAsync(System.Guid? facilityId = null);
        Task<IEnumerable<RestaurantOrder>> GetUnpaidOrdersAsync();
        Task<IEnumerable<RestaurantOrder>> GetTodayCompletedOrdersAsync(Guid facilityId, DateTime startDate, DateTime endDate);
        Task<IEnumerable<RestaurantOrder>> GetRecentOrdersAsync(Guid facilityId, int count);
        Task<IEnumerable<RestaurantOrder>> GetOrdersByRangeAsync(Guid facilityId, DateTime start, DateTime end);
    }
}
