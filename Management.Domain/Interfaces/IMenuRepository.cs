using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;

namespace Management.Domain.Interfaces
{
    public interface IMenuRepository : IRepository<RestaurantMenuItem>
    {
        Task<IEnumerable<RestaurantMenuItem>> GetByFacilityIdAsync(Guid facilityId);
    }
}
