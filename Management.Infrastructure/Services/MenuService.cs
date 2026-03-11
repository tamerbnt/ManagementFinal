using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Microsoft.Extensions.Logging;

namespace Management.Infrastructure.Services
{
    public class MenuService : IMenuService
    {
        private readonly IMenuRepository _menuRepository;
        private readonly ILogger<MenuService> _logger;

        public MenuService(IMenuRepository menuRepository, ILogger<MenuService> logger)
        {
            _menuRepository = menuRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<RestaurantMenuItemDto>> GetMenuItemsAsync(Guid facilityId)
        {
            try
            {
                var items = await _menuRepository.GetByFacilityIdAsync(facilityId);
                return items.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting menu items for facility {FacilityId}", facilityId);
                return Array.Empty<RestaurantMenuItemDto>();
            }
        }

        public async Task<RestaurantMenuItemDto?> GetMenuItemByIdAsync(Guid id)
        {
            try
            {
                var item = await _menuRepository.GetByIdAsync(id);
                return item != null ? MapToDto(item) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting menu item by id {MenuItemId}", id);
                return null;
            }
        }

        public async Task<bool> AddMenuItemAsync(RestaurantMenuItemDto dto)
        {
            try
            {
                var item = MapToEntity(dto);
                await _menuRepository.AddAsync(item);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding menu item {ItemName}", dto.Name);
                return false;
            }
        }

        public async Task<bool> UpdateMenuItemAsync(RestaurantMenuItemDto dto)
        {
            try
            {
                var item = await _menuRepository.GetByIdAsync(dto.Id);
                if (item == null) return false;

                item.Name = dto.Name;
                item.Category = dto.Category;
                item.Price = dto.Price;
                item.ImagePath = dto.ImagePath;
                item.IsAvailable = dto.IsAvailable;
                item.Ingredients = dto.Ingredients;

                await _menuRepository.UpdateAsync(item);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating menu item {MenuItemId}", dto.Id);
                return false;
            }
        }

        public async Task<bool> DeleteMenuItemAsync(Guid id)
        {
            try
            {
                await _menuRepository.DeleteAsync(id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting menu item {MenuItemId}", id);
                return false;
            }
        }

        private RestaurantMenuItemDto MapToDto(RestaurantMenuItem item)
        {
            return new RestaurantMenuItemDto
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                Price = item.Price,
                ImagePath = item.ImagePath,
                IsAvailable = item.IsAvailable,
                Ingredients = item.Ingredients,
                FacilityId = item.FacilityId,
                TenantId = item.TenantId
            };
        }

        private RestaurantMenuItem MapToEntity(RestaurantMenuItemDto dto)
        {
            return new RestaurantMenuItem
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                Name = dto.Name,
                Category = dto.Category,
                Price = dto.Price,
                ImagePath = dto.ImagePath,
                IsAvailable = dto.IsAvailable,
                Ingredients = dto.Ingredients,
                FacilityId = dto.FacilityId,
                TenantId = dto.TenantId
            };
        }
    }
}
