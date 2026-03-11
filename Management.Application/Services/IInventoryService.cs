using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Application.Services
{
    public interface IInventoryService
    {
        /// <summary>Returns all inventory resources, each with their cumulative total quantity.</summary>
        Task<IEnumerable<InventoryResourceDto>> GetResourcesAsync(Guid facilityId);

        /// <summary>Adds a new inventory resource (e.g. Tomatoes / kg).</summary>
        Task<bool> AddResourceAsync(InventoryResourceDto dto);

        /// <summary>Deletes an inventory resource and all its purchase history.</summary>
        Task<bool> DeleteResourceAsync(Guid resourceId);

        /// <summary>Returns all purchase log entries for a facility, ordered newest-first.</summary>
        Task<IEnumerable<InventoryPurchaseDto>> GetPurchaseHistoryAsync(Guid facilityId);

        /// <summary>Returns purchase log entries for a facility within a date range.</summary>
        Task<IEnumerable<InventoryPurchaseDto>> GetPurchasesByRangeAsync(Guid facilityId, DateTime start, DateTime end);

        /// <summary>Logs a new purchase entry (e.g. bought 5 kg of Tomatoes).</summary>
        Task<bool> LogPurchaseAsync(InventoryPurchaseDto dto);
    }
}
