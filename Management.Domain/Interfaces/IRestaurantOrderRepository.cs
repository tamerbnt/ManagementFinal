using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;

namespace Management.Domain.Interfaces
{
    public interface IRestaurantOrderRepository : IRepository<RestaurantOrder>
    {
        Task<IEnumerable<RestaurantOrder>> GetActiveOrdersAsync(System.Guid? facilityId = null);
        Task<int> GetNextDailyOrderNumberAsync(Guid facilityId);
    }
}
