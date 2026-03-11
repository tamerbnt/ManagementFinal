using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Services
{
    public interface IMenuService
    {
        Task<IEnumerable<RestaurantMenuItemDto>> GetMenuItemsAsync(Guid facilityId);
        Task<RestaurantMenuItemDto?> GetMenuItemByIdAsync(Guid id);
        Task<bool> AddMenuItemAsync(RestaurantMenuItemDto item);
        Task<bool> UpdateMenuItemAsync(RestaurantMenuItemDto item);
        Task<bool> DeleteMenuItemAsync(Guid id);
    }
}
